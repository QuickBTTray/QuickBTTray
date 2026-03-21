using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using QuickBTTrayApp.ViewModels;
using QuickBTTrayApp.Views;

namespace QuickBTTrayApp
{
    public partial class App : Application
    {
        private TaskbarIcon _trayIcon;
        private TrayMenuWindow _trayMenu;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            _trayMenu = new TrayMenuWindow(new TrayMenuViewModel());

            // RMB click opens the custom menu window
            _trayIcon.TrayRightMouseUp += (s, args) => _trayMenu.ShowNearTaskbar();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            _trayMenu?.Close();
            base.OnExit(e);
        }
    }
}