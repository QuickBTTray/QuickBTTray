using System.Diagnostics;

namespace QuickBTTrayApp.Services
{
    public sealed class AppLogger
    {
        public void Info(string message) => Debug.WriteLine($"[INFO]  {message}");
        public void Warn(string message) => Debug.WriteLine($"[WARN]  {message}");
        public void Error(string message, Exception? ex = null)
        {
            Debug.WriteLine($"[ERROR] {message}");
            if (ex is not null) Debug.WriteLine($"        {ex}");
        }
    }
}
