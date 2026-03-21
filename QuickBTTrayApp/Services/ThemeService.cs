using Microsoft.Win32;

namespace QuickBTTrayApp.Services
{
    public class ThemeService
    {
        public bool IsDarkMode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
    }
}
