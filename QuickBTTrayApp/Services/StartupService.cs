using Microsoft.Win32;

namespace QuickBTTrayApp.Services
{
    /// <summary>
    /// Reads and writes the HKCU Run registry key to control whether the app
    /// launches automatically when Windows starts.
    /// No admin rights required — HKCU is always writable by the current user.
    /// </summary>
    public sealed class StartupService
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName         = "QuickBTTray";
        private readonly AppLogger _logger;

        public StartupService(AppLogger logger) => _logger = logger;

        /// <summary>Returns true if the Run entry currently exists and points to this EXE.</summary>
        public bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex) { _logger.Error("StartupService: failed to read registry.", ex); return false; }
        }

        /// <summary>Adds or removes the HKCU Run entry.</summary>
        public void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                if (enable)
                {
                    var exePath = Environment.ProcessPath
                                  ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                                  ?? string.Empty;
                    key.SetValue(AppName, $"\"{exePath}\"");
                    _logger.Info($"StartupService: enabled (path={exePath}).");
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    _logger.Info("StartupService: disabled.");
                }
            }
            catch (Exception ex) { _logger.Error("StartupService: failed to write registry.", ex); }
        }
    }
}
