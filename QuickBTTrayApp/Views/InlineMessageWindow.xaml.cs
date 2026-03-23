using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using QuickBTTrayApp.Services;

namespace QuickBTTrayApp.Views
{
    public partial class InlineMessageWindow : Window
    {
        private readonly ThemeService _themeService = new();
        private readonly DispatcherTimer _hideTimer;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public InlineMessageWindow()
        {
            InitializeComponent();
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                Hide();
            };
        }

        public void ShowNearCursor(string message)
        {
            MessageText.Text = message;
            WindowHelper.ApplyTheme(this, _themeService.IsDarkMode());

            Show();
            UpdateLayout();

            GetCursorPos(out POINT cursor);
            WindowHelper.PositionNearPoint(this, cursor.X, cursor.Y);

            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }
}
