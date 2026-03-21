using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using QuickBTTrayApp.Models;

namespace QuickBTTrayApp.ViewModels
{
    public enum ConnectionMethod { UI, API }

    public class TrayMenuViewModel : INotifyPropertyChanged
    {
        private ConnectionMethod _connectBy = ConnectionMethod.UI;
        private ConnectionMethod _disconnectBy = ConnectionMethod.UI;

        public ObservableCollection<BluetoothDeviceViewModel> Devices { get; } = new();

        public ConnectionMethod ConnectBy
        {
            get => _connectBy;
            set { _connectBy = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectByUI)); OnPropertyChanged(nameof(ConnectByAPI)); }
        }

        public ConnectionMethod DisconnectBy
        {
            get => _disconnectBy;
            set { _disconnectBy = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisconnectByUI)); OnPropertyChanged(nameof(DisconnectByAPI)); }
        }

        public bool ConnectByUI    { get => ConnectBy == ConnectionMethod.UI;  set { if (value) ConnectBy = ConnectionMethod.UI; } }
        public bool ConnectByAPI   { get => ConnectBy == ConnectionMethod.API; set { if (value) ConnectBy = ConnectionMethod.API; } }
        public bool DisconnectByUI  { get => DisconnectBy == ConnectionMethod.UI;  set { if (value) DisconnectBy = ConnectionMethod.UI; } }
        public bool DisconnectByAPI { get => DisconnectBy == ConnectionMethod.API; set { if (value) DisconnectBy = ConnectionMethod.API; } }

        public ICommand ExitCommand { get; }
        public ICommand OpenBluetoothSettingsCommand { get; }

        public TrayMenuViewModel()
        {
            // Placeholder devices — will be replaced with real BT device discovery
            Devices.Add(new BluetoothDeviceViewModel(new BluetoothAudioDevice { Name = "BT Device 3", IsConnected = false }));
            Devices.Add(new BluetoothDeviceViewModel(new BluetoothAudioDevice { Name = "BT Device 2", IsConnected = true }));
            Devices.Add(new BluetoothDeviceViewModel(new BluetoothAudioDevice { Name = "BT Device 1", IsConnected = false }));

            ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
            OpenBluetoothSettingsCommand = new RelayCommand(_ =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:bluetooth",
                    UseShellExecute = true
                }));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
