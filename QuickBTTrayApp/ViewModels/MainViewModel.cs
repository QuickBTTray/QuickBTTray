using System.Collections.ObjectModel;
using QuickBTTrayApp.Models;

namespace QuickBTTrayApp.ViewModels
{
    public class MainViewModel
    {
        public ObservableCollection<BluetoothAudioDevice> Devices { get; set; } = new();
        public MainViewModel()
        {
            // Placeholder: Add sample devices
            Devices.Add(new BluetoothAudioDevice { Name = "BT Device 1", IsConnected = false });
            Devices.Add(new BluetoothAudioDevice { Name = "BT Device 2", IsConnected = true });
            Devices.Add(new BluetoothAudioDevice { Name = "BT Device 3", IsConnected = false });
        }
    }
}