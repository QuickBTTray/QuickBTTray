using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.ViewModels;

namespace QuickBTTrayApp.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ThemeService _themeService = new();
        private DateTime _lastDeactivated;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            Deactivated += (s, e) =>
            {
                _lastDeactivated = DateTime.UtcNow;
                Hide();
                _onHidden?.Invoke();
                _onHidden = null;
            };
        }

        private Action? _onHidden;

        public bool ShowNearCursor(Action? onHidden = null)
        {
            if ((DateTime.UtcNow - _lastDeactivated).TotalMilliseconds < 150) return false;
            _onHidden = onHidden;

            ApplyTheme();
            Show();
            UpdateLayout();

            GetCursorPos(out POINT cursor);
            PositionNearPoint(cursor.X, cursor.Y);
            Activate();
            return true;
        }

        private void PositionNearPoint(double anchorX, double anchorY)
        {
            var source = PresentationSource.FromVisual(this);
            Matrix fromDevice = source?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            double logicalX = anchorX * fromDevice.M11;
            double logicalY = anchorY * fromDevice.M22;

            var workArea = SystemParameters.WorkArea;

            double left = logicalX - ActualWidth / 2;
            double top  = logicalY - ActualHeight - 8;

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

        private void RadioOptionBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Child is RadioButton radio)
            {
                radio.IsChecked = true;
                e.Handled = true;
            }
        }
    }
}
