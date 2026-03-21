namespace QuickBTTrayApp.Services.Contracts
{
    public enum ToggleOutcome { Connected, Disconnected, Failed }

    public sealed record DeviceToggleResult(
        string DeviceName,
        string DeviceAddress,
        ToggleOutcome Outcome,
        string Message);
}
