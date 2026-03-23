using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using QuickBTTrayApp.Models;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.Services.Contracts;

namespace QuickBTTrayApp.ViewModels
{
    public enum ConnectionMethod { UI, API, HCI }

    public class TrayMenuViewModel : INotifyPropertyChanged
    {
        private readonly IBluetoothDeviceDiscovery _discovery;
        private readonly IBluetoothConnectPath     _apiConnect;
        private readonly IBluetoothConnectPath     _uiaConnect;
        private readonly IBluetoothDisconnectPath  _apiDisconnect;
        private readonly IBluetoothDisconnectPath  _uiaDisconnect;
        private readonly IBluetoothDisconnectPath  _hciDisconnect;
        private readonly AppStateStore             _stateStore;
        private readonly SemaphoreSlim             _operationGate = new(1, 1);
        private AppState         _appState;
        private bool             _isBusy;
        private ConnectionMethod _connectBy;
        private ConnectionMethod _disconnectBy;

        public ObservableCollection<BluetoothDeviceViewModel> Devices { get; } = new();

        /// <summary>Raised when a tray balloon notification should be shown.</summary>
        public event Action<string, string>? NotifyRequested;
        public event Action<string>? InlinePopupRequested;
        public event Action<bool>? BusyStateChanged;

        public bool NotificationsEnabled
        {
            get => _appState.EnableNotifications;
            set
            {
                if (_appState.EnableNotifications == value) return;
                _appState.EnableNotifications = value;
                _stateStore.Save(_appState);
                OnPropertyChanged();
            }
        }

        public ConnectionMethod ConnectBy
        {
            get => _connectBy;
            set
            {
                if (_connectBy == value) return;
                _connectBy = value;
                _appState.UseUiaConnect = value == ConnectionMethod.UI;
                _stateStore.Save(_appState);
                   OnPropertyChanged(); OnPropertyChanged(nameof(ConnectByUI)); OnPropertyChanged(nameof(ConnectByAPI));
            }
        }

        public ConnectionMethod DisconnectBy
        {
            get => _disconnectBy;
            set
            {
                if (_disconnectBy == value) return;
                _disconnectBy = value;
                _appState.UseUiaDisconnect = value == ConnectionMethod.UI;
                _appState.UseHciDisconnect = value == ConnectionMethod.HCI;
                _stateStore.Save(_appState);
                OnPropertyChanged(); OnPropertyChanged(nameof(DisconnectByUI)); OnPropertyChanged(nameof(DisconnectByAPI)); OnPropertyChanged(nameof(DisconnectByHCI));
            }
        }

        public bool ConnectByUI     { get => ConnectBy    == ConnectionMethod.UI;  set { if (value) ConnectBy    = ConnectionMethod.UI;  } }
        public bool ConnectByAPI    { get => ConnectBy    == ConnectionMethod.API; set { if (value) ConnectBy    = ConnectionMethod.API; } }
        public bool DisconnectByUI  { get => DisconnectBy == ConnectionMethod.UI;  set { if (value) DisconnectBy = ConnectionMethod.UI;  } }
        public bool DisconnectByAPI { get => DisconnectBy == ConnectionMethod.API; set { if (value) DisconnectBy = ConnectionMethod.API; } }
        public bool DisconnectByHCI { get => DisconnectBy == ConnectionMethod.HCI; set { if (value) DisconnectBy = ConnectionMethod.HCI; } }

        public ICommand ExitCommand                  { get; }
        public ICommand OpenBluetoothSettingsCommand { get; }

        public TrayMenuViewModel(
            IBluetoothDeviceDiscovery discovery,
            IBluetoothConnectPath     apiConnect,
            IBluetoothConnectPath     uiaConnect,
            IBluetoothDisconnectPath  apiDisconnect,
            IBluetoothDisconnectPath  uiaDisconnect,
            IBluetoothDisconnectPath  hciDisconnect,
               AppStateStore             stateStore)
        {
            _discovery     = discovery;
            _apiConnect    = apiConnect;
            _uiaConnect    = uiaConnect;
            _apiDisconnect = apiDisconnect;
            _uiaDisconnect = uiaDisconnect;
            _hciDisconnect = hciDisconnect;
            _stateStore    = stateStore;

            _appState     = stateStore.Load();
            _connectBy    = _appState.UseUiaConnect ? ConnectionMethod.UI
                         : ConnectionMethod.API;
            _disconnectBy = _appState.UseUiaDisconnect ? ConnectionMethod.UI
                          : _appState.UseHciDisconnect  ? ConnectionMethod.HCI
                          : ConnectionMethod.API;

            ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
            OpenBluetoothSettingsCommand = new RelayCommand(_ =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:bluetooth",
                    UseShellExecute = true
                }));
        }

        /// <summary>Refreshes Devices from the Win32 API. Call before showing the menu.</summary>
        public async Task RefreshDevicesAsync()
        {
            IReadOnlyList<BluetoothAudioDevice> raw;
            try   { raw = await Task.Run(() => _discovery.GetAudioDevices()); }
               catch { raw = []; }

            ApplyDiscoveredDevices(raw);
        }

        private void ApplyDiscoveredDevices(IReadOnlyList<BluetoothAudioDevice> raw)
        {

            // Prune selected addresses that no longer exist
            var active = raw.Select(d => d.Address).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _appState.SelectedDeviceAddresses.RemoveAll(a => !active.Contains(a));
            _stateStore.Save(_appState);

            // Disambiguate duplicate device names
            var dupes = raw
                .GroupBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

            Devices.Clear();
            foreach (var device in raw)
            {
                var displayName = dupes.Contains(device.Name) ? $"{device.Name} ({device.Address})" : device.Name;
                var vm = new BluetoothDeviceViewModel(device, displayName, ToggleDeviceAsync)
                {
                    IsSelected = _appState.SelectedDeviceAddresses
                        .Contains(device.Address, StringComparer.OrdinalIgnoreCase)
                };
                vm.PropertyChanged += OnDeviceSelectionChanged;
                Devices.Add(vm);
            }
        }

        private void ApplyToggleResultsToDevices(IReadOnlyList<DeviceToggleResult> results)
        {
            foreach (var result in results)
            {
                var vm = Devices.FirstOrDefault(d => string.Equals(d.Address, result.DeviceAddress, StringComparison.OrdinalIgnoreCase));
                if (vm == null) continue;

                if (result.Outcome == ToggleOutcome.Connected)
                {
                    vm.IsConnected = true;
                }
                else if (result.Outcome == ToggleOutcome.Disconnected)
                {
                    vm.IsConnected = false;
                }
            }
        }

        private async Task RefreshDevicesAfterToggleAsync(IReadOnlyList<DeviceToggleResult> results)
        {
            var expectedStates = results
                .Where(r => r.Outcome == ToggleOutcome.Connected || r.Outcome == ToggleOutcome.Disconnected)
                .ToDictionary(
                    r => r.DeviceAddress,
                    r => r.Outcome == ToggleOutcome.Connected,
                    StringComparer.OrdinalIgnoreCase);

            if (expectedStates.Count == 0)
            {
                await RefreshDevicesAsync();
                return;
            }

            IReadOnlyList<BluetoothAudioDevice> latest = [];

            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    latest = await Task.Run(() => _discovery.GetAudioDevices());
                }
                catch
                {
                    latest = [];
                }

                var actualStates = latest.ToDictionary(d => d.Address, d => d.IsConnected, StringComparer.OrdinalIgnoreCase);
                var settled = expectedStates.All(expected =>
                    actualStates.TryGetValue(expected.Key, out var isConnected) && isConnected == expected.Value);

                if (settled)
                {
                    ApplyDiscoveredDevices(latest);
                    return;
                }

                await Task.Delay(350);
            }

            ApplyDiscoveredDevices(latest);
        }

        /// <summary>LMB single-click: batch connect/disconnect all selected devices.</summary>
        public async Task OnTrayLeftSingleClickAsync()
        {
            if (_appState.SelectedDeviceAddresses.Count == 0)
            {
                InlinePopupRequested?.Invoke("No devices ✅ selectd to Connect/Disconnect in the right-click menu");
                return;
            }

            if (!await _operationGate.WaitAsync(0))
            {
                Notify("QuickBTTray", "A Bluetooth action is already running.");
                return;
            }

            SetBusy(true);
            try
            {
                var fresh    = await Task.Run(() => _discovery.GetAudioDevices());
                var selected = fresh
                    .Where(d => _appState.SelectedDeviceAddresses
                        .Contains(d.Address, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (selected.Count == 0) return;

                bool allConnected = selected.All(d => d.IsConnected);

                var results = new List<DeviceToggleResult>();
                foreach (var d in selected)
                    results.Add(allConnected
                        ? await DispatchDisconnectAsync(d.Name, d.Address)
                        : await DispatchConnectAsync(d.Name, d.Address));

                HandleResults(results);
                ApplyToggleResultsToDevices(results);
                await RefreshDevicesAfterToggleAsync(results);
            }
               catch (Exception ex) { Notify("QuickBTTray", ex.Message); }
            finally
            {
                SetBusy(false);
                _operationGate.Release();
            }
        }

        // ── Internal ─────────────────────────────────────────────────────────
        private async Task ToggleDeviceAsync(BluetoothDeviceViewModel vm)
        {
            if (!await _operationGate.WaitAsync(0))
            {
                Notify("QuickBTTray", "A Bluetooth action is already running.");
                return;
            }

            SetBusy(true);
            try
            {
                var result = vm.IsConnected
                    ? await DispatchDisconnectAsync(vm.RawName, vm.Address)
                    : await DispatchConnectAsync(vm.RawName, vm.Address);
                HandleResults([result]);
                ApplyToggleResultsToDevices([result]);
                await RefreshDevicesAfterToggleAsync([result]);
            }
               catch (Exception ex) { Notify("QuickBTTray", ex.Message); }
            finally
            {
                SetBusy(false);
                _operationGate.Release();
            }
        }

        private void SetBusy(bool isBusy)
        {
            if (_isBusy == isBusy) return;
            _isBusy = isBusy;
            BusyStateChanged?.Invoke(isBusy);
        }

        private void OnDeviceSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BluetoothDeviceViewModel.IsSelected)) return;
            var dvm = (BluetoothDeviceViewModel)sender!;
            if (dvm.IsSelected)
            {
                if (!_appState.SelectedDeviceAddresses
                    .Contains(dvm.Address, StringComparer.OrdinalIgnoreCase))
                    _appState.SelectedDeviceAddresses.Add(dvm.Address);
            }
            else
            {
                _appState.SelectedDeviceAddresses
                    .RemoveAll(a => string.Equals(a, dvm.Address, StringComparison.OrdinalIgnoreCase));
            }
            _stateStore.Save(_appState);
        }

        private async Task<DeviceToggleResult> DispatchConnectAsync(string name, string addr)
        {
            var pathLabel = ConnectBy switch
            {
                ConnectionMethod.UI  => "UI",
                _                    => "API",
            };
            Notify($"Connecting ({pathLabel})", name);

            var result = await (ConnectBy switch
            {
                ConnectionMethod.UI  => _uiaConnect.ConnectAsync(name, addr),
                _                    => _apiConnect.ConnectAsync(name, addr),
            });

            if (result.Outcome == ToggleOutcome.Failed)
                Notify($"Connect failed ({pathLabel})", $"{name}\n{result.Message}");

            return result;
        }

        private async Task<DeviceToggleResult> DispatchDisconnectAsync(string name, string addr)
        {
            var pathLabel = DisconnectBy switch
            {
                ConnectionMethod.UI  => "UI",
                ConnectionMethod.HCI => "HCI",
                _                    => "API",
            };
            Notify($"Disconnecting ({pathLabel})", name);

            var result = await (DisconnectBy switch
            {
                ConnectionMethod.UI  => _uiaDisconnect.DisconnectAsync(name, addr),
                ConnectionMethod.HCI => _hciDisconnect.DisconnectAsync(name, addr),
                _                    => _apiDisconnect.DisconnectAsync(name, addr),
            });

            if (result.Outcome == ToggleOutcome.Failed)
                Notify($"Disconnect failed ({pathLabel})", $"{name}\n{result.Message}");

            return result;
        }

        private void HandleResults(IReadOnlyList<DeviceToggleResult> results)
        {
            var failed = results.Where(r => r.Outcome == ToggleOutcome.Failed).ToList();
            if (failed.Count > 1)
            {
                var msg = $"Failed: {string.Join(", ", failed.Select(r => r.DeviceName))}. {failed[0].Message}";
                Notify("QuickBTTray", msg);
            }
        }

        private void Notify(string title, string msg)
        {
            if (!NotificationsEnabled) return;
            NotifyRequested?.Invoke(title, msg);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
