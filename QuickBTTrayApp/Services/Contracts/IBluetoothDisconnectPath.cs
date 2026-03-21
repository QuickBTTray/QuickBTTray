namespace QuickBTTrayApp.Services.Contracts
{
    /// <summary>Disconnects one BT device. Separate interface so API or UI path can be removed independently.</summary>
    public interface IBluetoothDisconnectPath
    {
        Task<DeviceToggleResult> DisconnectAsync(string deviceName, string deviceAddress);
    }
}
