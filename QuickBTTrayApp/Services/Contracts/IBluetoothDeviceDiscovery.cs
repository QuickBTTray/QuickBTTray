using QuickBTTrayApp.Models;

namespace QuickBTTrayApp.Services.Contracts
{
    /// <summary>Discovers paired Bluetooth audio devices. Only the API path implements this.</summary>
    public interface IBluetoothDeviceDiscovery
    {
        IReadOnlyList<BluetoothAudioDevice> GetAudioDevices();
    }
}
