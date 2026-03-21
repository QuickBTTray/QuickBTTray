using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuickBTTrayApp.Models;

namespace QuickBTTrayApp.ViewModels
{
    public class BluetoothDeviceViewModel : INotifyPropertyChanged
    {
        private readonly Func<BluetoothDeviceViewModel, Task> _toggleAction;
        private bool _isConnected;
        private bool _isSelected;

        public string Address { get; }
        public string RawName { get; }   // actual BT device name — used for UIA lookups
        public string Name    { get; }   // display name — may include address suffix for duplicates

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(ConnectionButtonLabel)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string StatusIcon => IsConnected
            ? "pack://application:,,,/Views/Assets/device-connected.png"
            : "pack://application:,,,/Views/Assets/device-disconnected.png";

        public string ConnectionButtonLabel => IsConnected ? "Disconnect" : "Connect";

        public ICommand ToggleConnectionCommand { get; }
        public ICommand ToggleSelectedCommand   { get; }

        public BluetoothDeviceViewModel(
            BluetoothAudioDevice device,
            string displayName,
            Func<BluetoothDeviceViewModel, Task> toggleAction)
        {
            Address       = device.Address;
            RawName       = device.Name;
            Name          = displayName;
            IsConnected   = device.IsConnected;
            IsSelected    = device.IsSelected;
            _toggleAction = toggleAction;

            ToggleConnectionCommand = new RelayCommand(async _ =>
            {
                try { await _toggleAction(this); }
                catch { /* exceptions handled in TrayMenuViewModel */ }
            });
            ToggleSelectedCommand = new RelayCommand(_ => IsSelected = !IsSelected);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
