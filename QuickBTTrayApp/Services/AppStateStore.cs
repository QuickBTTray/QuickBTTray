using System.IO;
using System.Text.Json;

namespace QuickBTTrayApp.Services
{
    public sealed class AppState
    {
        public List<string> SelectedDeviceAddresses { get; init; } = [];
        public bool EnableNotifications { get; set; } = true;
        public bool SendMediaPlayOnConnect { get; set; } = false;
        public bool SendMediaPauseOnDisconnect { get; set; } = false;
        public bool UseUiaConnect     { get; set; } = false;
        /// <summary>When true, disconnect uses the UI Automation path (otherwise HCI).</summary>
        public bool UseUiaDisconnect  { get; set; } = false;
        /// <summary>When true, disconnect uses the HCI IOCTL path (default, most reliable).</summary>
        public bool UseHciDisconnect  { get; set; } = true;
        public static AppState CreateDefault() => new();
    }

    public sealed class AppStateStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private readonly string _stateFilePath;

           public AppStateStore()
           {
               var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickBTTray");
            _stateFilePath = Path.Combine(dir, "tray-state.json");
        }

        public AppState Load()
        {
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    return AppState.CreateDefault();
                }
                var json = File.ReadAllText(_stateFilePath);
                return JsonSerializer.Deserialize<AppState>(json, SerializerOptions) ?? AppState.CreateDefault();
            }
               catch { return AppState.CreateDefault(); }
        }

        public void Save(AppState state)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
                File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(state, SerializerOptions));
            }
               catch { }
        }
    }
}
