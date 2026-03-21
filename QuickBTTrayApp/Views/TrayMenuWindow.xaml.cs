using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.ViewModels;

namespace QuickBTTrayApp.Views
{
    public partial class TrayMenuWindow : Window
    {
        private readonly ThemeService _themeService = new();
        private DateTime _lastDeactivated;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public TrayMenuWindow(TrayMenuViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            Deactivated += (s, e) =>
            {
                _lastDeactivated = DateTime.UtcNow;
                Hide();
            };
        }

        public void ShowNearTaskbar()
        {
            if ((DateTime.UtcNow - _lastDeactivated).TotalMilliseconds < 150) return;

            ApplyTheme();
            Show();
            UpdateLayout();

            GetCursorPos(out POINT cursor);
            PositionNearPoint(cursor.X, cursor.Y);
            Activate();
        }

        private void PositionNearPoint(double anchorX, double anchorY)
        {
            // GetCursorPos returns physical (device) pixels; WPF Left/Top use logical pixels.
            // Use the presentation source transform to convert, handling any DPI scale.
            var source = PresentationSource.FromVisual(this);
            Matrix fromDevice = source?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            double logicalX = anchorX * fromDevice.M11;
            double logicalY = anchorY * fromDevice.M22;

            var workArea = SystemParameters.WorkArea;

            // Center horizontally on anchor; place above it (near taskbar)
            double left = logicalX - ActualWidth / 2;
            double top  = logicalY - ActualHeight - 8;

            // Clamp so the menu stays fully within the work area
            left = Math.Max(workArea.Left + 4, Math.Min(left, workArea.Right  - ActualWidth  - 4));
            top  = Math.Max(workArea.Top  + 4, Math.Min(top,  workArea.Bottom - ActualHeight - 4));

            Left = left;
            Top  = top;
        }

        private void ApplyTheme()
        {
            bool isDark = _themeService.IsDarkMode();
            var themeUri = isDark
                ? new Uri("pack://application:,,,/Views/Themes/DarkTheme.xaml")
                : new Uri("pack://application:,,,/Views/Themes/LightTheme.xaml");

            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }
    }
}
