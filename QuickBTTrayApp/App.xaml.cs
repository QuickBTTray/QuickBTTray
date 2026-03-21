using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using QuickBTTrayApp.Services;
using QuickBTTrayApp.Services.Api;
using QuickBTTrayApp.Services.Ui;
using QuickBTTrayApp.ViewModels;
using QuickBTTrayApp.Views;

namespace QuickBTTrayApp
{
    public partial class App : Application
    {
        private TaskbarIcon       _trayIcon        = null!;
        private TrayMenuWindow    _trayMenu        = null!;
        private TrayMenuViewModel _viewModel       = null!;
        private DispatcherTimer   _singleClickTimer = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logger     = new AppLogger();
            var stateStore = new AppStateStore(logger);
            var apiService = new BluetoothApiService(logger);
            var uiaService = new BluetoothUiaService(logger);

            _viewModel = new TrayMenuViewModel(
                discovery:     apiService,
                apiConnect:    apiService,
                uiaConnect:    uiaService,
                apiDisconnect: apiService,
                uiaDisconnect: uiaService,
                stateStore:    stateStore,
                logger:        logger);

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            _trayMenu = new TrayMenuWindow(_viewModel);

            // Balloon notifications from ViewModel
            _viewModel.NotifyRequested += (title, msg) =>
                _trayIcon.ShowBalloonTip(title, msg, BalloonIcon.Info);

            // RMB: refresh device list then show menu
            _trayIcon.TrayRightMouseUp += async (s, args) =>
            {
                await _viewModel.RefreshDevicesAsync();
                _trayMenu.ShowNearTaskbar();
            };

            // LMB single/double click discrimination (300 ms timer)
            _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _singleClickTimer.Tick += async (s, args) =>
            {
                _singleClickTimer.Stop();
                await _viewModel.OnTrayLeftSingleClickAsync();
            };

            _trayIcon.TrayLeftMouseUp += (s, args) =>
            {
                _singleClickTimer.Stop();
                _singleClickTimer.Start();
            };

            _trayIcon.TrayMouseDoubleClick += (s, args) =>
            {
                _singleClickTimer.Stop();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:bluetooth",
                    UseShellExecute = true
                });
            };

            logger.Info("Application started.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _singleClickTimer?.Stop();
            _trayIcon?.Dispose();
            _trayMenu?.Close();
            base.OnExit(e);
        }
    }
}