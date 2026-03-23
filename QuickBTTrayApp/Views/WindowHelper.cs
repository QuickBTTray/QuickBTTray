using System.Windows;
using System.Windows.Media;

namespace QuickBTTrayApp.Views
{
    /// <summary>
    /// Shared helper methods for window positioning and theming.
    /// Reduces code duplication across TrayMenuWindow and SettingsWindow.
    /// </summary>
    public static class WindowHelper
    {
        /// <summary>
        /// Positions a window near the given anchor point, accounting for DPI scaling and work area bounds.
        /// </summary>
        public static void PositionNearPoint(Window window, double anchorX, double anchorY)
        {
            // GetCursorPos returns physical (device) pixels; WPF Left/Top use logical pixels.
            // Use the presentation source transform to convert, handling any DPI scale.
            var source = PresentationSource.FromVisual(window);
            Matrix fromDevice = source?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            double logicalX = anchorX * fromDevice.M11;
            double logicalY = anchorY * fromDevice.M22;

            var workArea = SystemParameters.WorkArea;

            // Center horizontally on anchor; place above it (near taskbar)
            double left = logicalX - window.ActualWidth / 2;
            double top  = logicalY - window.ActualHeight - 8;

            // Clamp so the window stays fully within the work area
            left = Math.Max(workArea.Left + 4, Math.Min(left, workArea.Right  - window.ActualWidth  - 4));
            top  = Math.Max(workArea.Top  + 4, Math.Min(top,  workArea.Bottom - window.ActualHeight - 4));

            window.Left = left;
            window.Top  = top;
        }

        /// <summary>
        /// Applies the current system theme (dark/light) to the window.
        /// </summary>
        public static void ApplyTheme(Window window, bool isDarkMode)
        {
            var themeUri = isDarkMode
                ? new Uri("pack://application:,,,/Views/Themes/DarkTheme.xaml")
                : new Uri("pack://application:,,,/Views/Themes/LightTheme.xaml");

            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }
    }
}
