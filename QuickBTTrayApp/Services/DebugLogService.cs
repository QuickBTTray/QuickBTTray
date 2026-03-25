using System.Diagnostics;
using System.IO;

namespace QuickBTTrayApp.Services
{
    internal static class DebugLogService
    {
        private static readonly object SyncRoot = new();
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickBTTray",
            "debug.log");

        public static string CurrentLogFilePath => LogFilePath;

        public static void Log(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            Debug.WriteLine(line);

            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}