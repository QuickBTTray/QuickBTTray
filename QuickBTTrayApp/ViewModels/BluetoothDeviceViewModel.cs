using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuickBTTrayApp.Models;

namespace QuickBTTrayApp.ViewModels
{
    public class BluetoothDeviceViewModel : INotifyPropertyChanged
    {
        private bool _isConnected;
        private bool _isSelected;

        public string Name { get; set; }

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
        public ICommand ToggleSelectedCommand { get; }

        public BluetoothDeviceViewModel(BluetoothAudioDevice device)
        {
            Name = device.Name;
            IsConnected = device.IsConnected;
            IsSelected = device.IsSelected;
            ToggleConnectionCommand = new RelayCommand(_ => ToggleConnection());
            ToggleSelectedCommand = new RelayCommand(_ => IsSelected = !IsSelected);
        }

        // Stub — will call BluetoothDeviceService in the business logic phase
        private void ToggleConnection() { IsConnected = !IsConnected; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
