namespace QuickBTTrayApp.Services.Contracts
{
    /// <summary>Connects one BT device. Separate interface so API or UI path can be removed independently.</summary>
    public interface IBluetoothConnectPath
    {
        Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress);
    }
}
