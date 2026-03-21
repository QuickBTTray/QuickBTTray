using System.Windows;

namespace QuickBTTrayApp
{
    public partial class App : Application
    {
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _trayIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("TrayIcon");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        private void TrayMenu_Exit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Shutdown();
        }
    }
}