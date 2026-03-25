using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.ViewModels;

namespace QuickBTTrayApp.Views
{
    public partial class TrayMenuWindow : Window
    {
        private readonly ThemeService _themeService = new();
        private readonly DispatcherTimer _suppressDeactivateTimeout;
        private DateTime _lastDeactivated;
        private double _lastKnownWidth;
        private double _lastKnownHeight;
        private SettingsWindow _settingsWindow = null!;
        private bool _suppressDeactivate;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private enum TaskbarEdge
        {
            Unknown,
            Left,
            Top,
            Right,
            Bottom
        }

        public TrayMenuWindow(TrayMenuViewModel viewModel, SettingsWindow settingsWindow)
        {
            InitializeComponent();
            DataContext = viewModel;
            _settingsWindow = settingsWindow;
            _suppressDeactivateTimeout = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _suppressDeactivateTimeout.Tick += (s, e) =>
            {
                _suppressDeactivateTimeout.Stop();
                _suppressDeactivate = false;

                // If settings is still open, keep the main menu visible.
                // If settings is already hidden and focus is elsewhere, close the main menu.
                if (!_settingsWindow.IsVisible && !IsActive)
                {
                    _lastDeactivated = DateTime.UtcNow;
                    Hide();
                }
            };

            Deactivated += (s, e) =>
            {
                if (_suppressDeactivate) return;
                _lastDeactivated = DateTime.UtcNow;
                Hide();
            };
        }

        private void GearButton_Click(object sender, RoutedEventArgs e)
        {
            // Suppress the Deactivated hide that fires when focus moves to the settings window
            _suppressDeactivate = true;
            var shown = _settingsWindow.ShowNearCursor(onHidden: () =>
            {
                ReleaseSuppressAndHideIfInactive();
            });

            if (!shown)
            {
                _suppressDeactivate = false;
                return;
            }

            _suppressDeactivateTimeout.Stop();
            _suppressDeactivateTimeout.Start();
        }

        private void ReleaseSuppressAndHideIfInactive()
        {
            _suppressDeactivate = false;

            // After WPF finishes processing focus/activation events, check whether
            // the main menu got focus (user clicked inside it) or focus went elsewhere.
            // If focus went elsewhere, close the main menu too.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!IsActive)
                {
                    _lastDeactivated = DateTime.UtcNow;
                    Hide();
                }
            }));
        }

        public void ShowNearTaskbar(double? anchorX = null, double? anchorY = null)
        {
            if ((DateTime.UtcNow - _lastDeactivated).TotalMilliseconds < 150) return;

            ApplyTheme();

            double resolvedAnchorX;
            double resolvedAnchorY;

            if (anchorX.HasValue && anchorY.HasValue)
            {
                resolvedAnchorX = anchorX.Value;
                resolvedAnchorY = anchorY.Value;
            }
            else if (TryGetTrayAnchor(out var trayAnchorX, out var trayAnchorY))
            {
                resolvedAnchorX = trayAnchorX;
                resolvedAnchorY = trayAnchorY;
            }
            else
            {
                GetCursorPos(out POINT cursor);
                resolvedAnchorX = cursor.X;
                resolvedAnchorY = cursor.Y;
            }

            // Use cached dimensions to position before Show, eliminating visible move on reopen.
            if (_lastKnownWidth > 0 && _lastKnownHeight > 0)
            {
                PositionNearTrayIcon(resolvedAnchorX, resolvedAnchorY, _lastKnownWidth, _lastKnownHeight);
                Opacity = 1;
            }
            else
            {
                // First show: no known size yet, so keep hidden while measuring and positioning.
                Opacity = 0;
            }

            Show();
            UpdateLayout();

            // First show or post-theme size change: recalculate with actual size.
            if (_lastKnownWidth <= 0 || _lastKnownHeight <= 0 ||
                Math.Abs(ActualWidth - _lastKnownWidth) > 0.5 ||
                Math.Abs(ActualHeight - _lastKnownHeight) > 0.5)
            {
                PositionNearTrayIcon(resolvedAnchorX, resolvedAnchorY, ActualWidth, ActualHeight);
            }

            _lastKnownWidth = ActualWidth;
            _lastKnownHeight = ActualHeight;

            Opacity = 1;
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

        private void PositionNearTrayIcon(double anchorX, double anchorY, double widthLogical, double heightLogical)
        {
            // Use monitor-local taskbar edge to keep menu attached to tray area on multi-monitor setups.
            var source = PresentationSource.FromVisual(this);
            Matrix fromDevice = source?.CompositionTarget.TransformFromDevice ?? Matrix.Identity;
            Matrix toDevice = source?.CompositionTarget.TransformToDevice ?? Matrix.Identity;

            var screen = Forms.Screen.FromPoint(new Drawing.Point((int)anchorX, (int)anchorY));
            var boundsPx = screen.Bounds;
            var workPx = screen.WorkingArea;
            var edge = GetTaskbarEdge(boundsPx, workPx);

            var widthPx = widthLogical * toDevice.M11;
            var heightPx = heightLogical * toDevice.M22;

            double leftPx;
            double topPx;

            switch (edge)
            {
                case TaskbarEdge.Top:
                    leftPx = anchorX - (widthPx / 2);
                    topPx = anchorY + 8;
                    break;
                case TaskbarEdge.Left:
                    leftPx = anchorX + 8;
                    topPx = anchorY - (heightPx / 2);
                    break;
                case TaskbarEdge.Right:
                    leftPx = anchorX - widthPx - 8;
                    topPx = anchorY - (heightPx / 2);
                    break;
                case TaskbarEdge.Bottom:
                case TaskbarEdge.Unknown:
                default:
                    leftPx = anchorX - (widthPx / 2);
                    topPx = anchorY - heightPx - 8;
                    break;
            }

            // Clamp to monitor work area in device pixels.
            leftPx = Math.Max(workPx.Left + 4, Math.Min(leftPx, workPx.Right - widthPx - 4));
            topPx = Math.Max(workPx.Top + 4, Math.Min(topPx, workPx.Bottom - heightPx - 4));

            Left = leftPx * fromDevice.M11;
            Top = topPx * fromDevice.M22;
        }

        private static TaskbarEdge GetTaskbarEdge(Drawing.Rectangle boundsPx, Drawing.Rectangle workPx)
        {
            if (workPx.Left > boundsPx.Left) return TaskbarEdge.Left;
            if (workPx.Top > boundsPx.Top) return TaskbarEdge.Top;
            if (workPx.Right < boundsPx.Right) return TaskbarEdge.Right;
            if (workPx.Bottom < boundsPx.Bottom) return TaskbarEdge.Bottom;
            return TaskbarEdge.Unknown;
        }

        private static bool TryGetTrayAnchor(out double anchorX, out double anchorY)
        {
            anchorX = 0;
            anchorY = 0;

            var shellTray = FindWindow("Shell_TrayWnd", null);
            if (shellTray == nint.Zero) return false;

            var trayNotify = FindWindowEx(shellTray, nint.Zero, "TrayNotifyWnd", null);
            if (trayNotify == nint.Zero) return false;

            nint notifyArea = FindWindowEx(trayNotify, nint.Zero, "SysPager", null);
            if (notifyArea != nint.Zero)
            {
                notifyArea = FindWindowEx(notifyArea, nint.Zero, "ToolbarWindow32", null);
            }
            else
            {
                notifyArea = FindWindowEx(trayNotify, nint.Zero, "ToolbarWindow32", null);
            }

            if (notifyArea == nint.Zero || !GetWindowRect(notifyArea, out RECT rect))
            {
                if (!GetWindowRect(trayNotify, out rect)) return false;
            }

            var bounds = Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            var screen = Forms.Screen.FromRectangle(bounds);
            var edge = GetTaskbarEdge(screen.Bounds, screen.WorkingArea);

            switch (edge)
            {
                case TaskbarEdge.Top:
                    anchorX = rect.Right - 8;
                    anchorY = rect.Bottom - 4;
                    break;
                case TaskbarEdge.Left:
                    anchorX = rect.Right - 4;
                    anchorY = rect.Bottom - 8;
                    break;
                case TaskbarEdge.Right:
                    anchorX = rect.Left + 4;
                    anchorY = rect.Bottom - 8;
                    break;
                case TaskbarEdge.Bottom:
                case TaskbarEdge.Unknown:
                default:
                    anchorX = rect.Right - 8;
                    anchorY = rect.Top + 4;
                    break;
            }

            return true;
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
