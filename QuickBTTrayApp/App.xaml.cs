using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.Services.Api;
using QuickBTTrayApp.Services.Api2;
using QuickBTTrayApp.Services.Ui;
using QuickBTTrayApp.ViewModels;
using QuickBTTrayApp.Views;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickBTTrayApp
{
    public partial class App : Application
    {
        private TaskbarIcon       _trayIcon        = null!;
        private TrayMenuWindow    _trayMenu        = null!;
        private TrayMenuViewModel _viewModel       = null!;
        private SettingsWindow    _settingsWindow  = null!;
        private InlineMessageWindow _inlineMessageWindow = null!;
        private DispatcherTimer   _busyBlinkTimer   = null!;
        private ImageSource       _defaultTrayIconSource = null!;
        private ImageSource       _connectingTrayIconSource = null!;
        private bool              _showConnectingIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApplyInitialThemeResources();

            var stateStore     = new AppStateStore();
            var startupService = new StartupService();
            var apiService     = new BluetoothApiService();
            var uiaService     = new BluetoothUiaService();
            var hciService     = new BluetoothHciService();

            _viewModel = new TrayMenuViewModel(
                discovery:     apiService,
                apiConnect:    apiService,
                uiaConnect:    uiaService,
                apiDisconnect: apiService,
                uiaDisconnect: uiaService,
                hciDisconnect: hciService,
                   stateStore:    stateStore);

            var settingsViewModel = new SettingsViewModel(startupService, _viewModel);
            _settingsWindow = new SettingsWindow(settingsViewModel);
            _inlineMessageWindow = new InlineMessageWindow();

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            _trayMenu = new TrayMenuWindow(_viewModel, _settingsWindow);
            _defaultTrayIconSource = _trayIcon.IconSource;
            _connectingTrayIconSource = BitmapFrame.Create(
                new Uri("pack://application:,,,/Views/Assets/icon-connecting.ico", UriKind.Absolute));
            _busyBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _busyBlinkTimer.Tick += (s, args) => ToggleBusyTrayIcon();

            // Balloon notifications from ViewModel
            _viewModel.NotifyRequested += (title, msg) =>
                _trayIcon.ShowBalloonTip(title, msg, BalloonIcon.Info);
            _viewModel.InlinePopupRequested += msg =>
                Dispatcher.Invoke(() => _inlineMessageWindow.ShowNearCursor(msg));
            _viewModel.BusyStateChanged += isBusy =>
            {
                if (isBusy) StartBusyTrayAnimation();
                else StopBusyTrayAnimation();
            };

            // RMB: refresh device list then show menu
            _trayIcon.TrayRightMouseUp += async (s, args) =>
            {
                await _viewModel.RefreshDevicesAsync();
                _trayMenu.ShowNearTaskbar();
            };

            // LMB: trigger selected-device action immediately.
            _trayIcon.TrayLeftMouseUp += async (s, args) =>
            {
                await _viewModel.OnTrayLeftSingleClickAsync();
            };

        }

        private void ApplyInitialThemeResources()
        {
            var themeService = new ThemeService();
            var themeUri = themeService.IsDarkMode()
                ? new Uri("pack://application:,,,/Views/Themes/DarkTheme.xaml", UriKind.Absolute)
                : new Uri("pack://application:,,,/Views/Themes/LightTheme.xaml", UriKind.Absolute);

            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _busyBlinkTimer?.Stop();
            _trayIcon?.Dispose();
            _trayMenu?.Close();
            _settingsWindow?.Close();
            _inlineMessageWindow?.Close();
            base.OnExit(e);
        }

        private void StartBusyTrayAnimation()
        {
            _showConnectingIcon = true;
            _trayIcon.IconSource = _connectingTrayIconSource;
            _busyBlinkTimer.Start();
        }

        private void StopBusyTrayAnimation()
        {
            _busyBlinkTimer.Stop();
            _showConnectingIcon = false;
            _trayIcon.IconSource = _defaultTrayIconSource;
        }

        private void ToggleBusyTrayIcon()
        {
            _showConnectingIcon = !_showConnectingIcon;
            _trayIcon.IconSource = _showConnectingIcon
                ? _connectingTrayIconSource
                : _defaultTrayIconSource;
        }
    }
}