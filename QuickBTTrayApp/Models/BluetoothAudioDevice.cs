namespace QuickBTTrayApp.Models
{
    public class BluetoothAudioDevice
    {
        public string Name              { get; set; } = string.Empty;
        public string Address           { get; set; } = string.Empty;
        public bool   IsConnected       { get; set; }
        public bool   IsSelected        { get; set; }
        public bool   SupportsHandsfree { get; set; }
        public bool   SupportsAudioSink { get; set; }
    }
}