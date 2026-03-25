using System.Diagnostics;
using System.IO;

namespace QuickBTTrayApp.Services
{
    /// <summary>
    /// Debug-only logging. All call sites are compiled out in Release builds via
    /// <see cref="ConditionalAttribute"/>. Add calls freely — zero cost in production.
    /// </summary>
    internal static class DebugLogService
    {
        private static readonly object SyncRoot = new();
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickBTTray",
            "debug.log");

        public static string CurrentLogFilePath => LogFilePath;

        [Conditional("DEBUG")]
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