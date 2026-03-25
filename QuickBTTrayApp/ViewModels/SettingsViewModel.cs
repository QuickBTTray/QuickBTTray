using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuickBTTrayApp.Services;

namespace QuickBTTrayApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly StartupService _startupService;
        private readonly TrayMenuViewModel _trayMenuViewModel;
        private bool _runOnStartup;

        public bool RunOnStartup
        {
            get => _runOnStartup;
            set
            {
                if (_runOnStartup == value) return;
                _runOnStartup = value;
                _startupService.SetEnabled(value);
                OnPropertyChanged();
            }
        }

        public bool NotificationsEnabled
        {
            get => _trayMenuViewModel.NotificationsEnabled;
            set
            {
                if (_trayMenuViewModel.NotificationsEnabled == value) return;
                _trayMenuViewModel.NotificationsEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool SendMediaPauseOnDisconnect
        {
            get => _trayMenuViewModel.SendMediaPauseOnDisconnect;
            set
            {
                if (_trayMenuViewModel.SendMediaPauseOnDisconnect == value) return;
                _trayMenuViewModel.SendMediaPauseOnDisconnect = value;
                OnPropertyChanged();
            }
        }

        public bool SendMediaPlayOnConnect
        {
            get => _trayMenuViewModel.SendMediaPlayOnConnect;
            set
            {
                if (_trayMenuViewModel.SendMediaPlayOnConnect == value) return;
                _trayMenuViewModel.SendMediaPlayOnConnect = value;
                OnPropertyChanged();
            }
        }

        public string AppVersion =>
            "QuickBTTray-v" + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0");

        public ICommand OpenGitHubCommand { get; } = new RelayCommand(_ =>
            Process.Start(new ProcessStartInfo("https://github.com/QuickBTTray/QuickBTTray") { UseShellExecute = true }));

        public ICommand ToggleRunOnStartupCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
        public ICommand ToggleSendMediaPlayOnConnectCommand { get; }
        public ICommand ToggleSendMediaPauseOnDisconnectCommand { get; }
        public ICommand OpenBluetoothSettingsCommand => _trayMenuViewModel.OpenBluetoothSettingsCommand;

        public ConnectionMethod ConnectBy
        {
            get => _trayMenuViewModel.ConnectBy;
            set => _trayMenuViewModel.ConnectBy = value;
        }

        public ConnectionMethod DisconnectBy
        {
            get => _trayMenuViewModel.DisconnectBy;
            set => _trayMenuViewModel.DisconnectBy = value;
        }

        public SettingsViewModel(StartupService startupService, TrayMenuViewModel trayMenuViewModel)
        {
            _startupService = startupService;
            _trayMenuViewModel = trayMenuViewModel;
            _runOnStartup = startupService.IsEnabled();
            ToggleRunOnStartupCommand = new RelayCommand(_ => RunOnStartup = !RunOnStartup);
            ToggleNotificationsCommand = new RelayCommand(_ => NotificationsEnabled = !NotificationsEnabled);
            ToggleSendMediaPlayOnConnectCommand = new RelayCommand(_ => SendMediaPlayOnConnect = !SendMediaPlayOnConnect);
            ToggleSendMediaPauseOnDisconnectCommand = new RelayCommand(_ => SendMediaPauseOnDisconnect = !SendMediaPauseOnDisconnect);

            _trayMenuViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrayMenuViewModel.ConnectBy))
                    OnPropertyChanged(nameof(ConnectBy));
                else if (e.PropertyName == nameof(TrayMenuViewModel.DisconnectBy))
                    OnPropertyChanged(nameof(DisconnectBy));
                else if (e.PropertyName == nameof(TrayMenuViewModel.NotificationsEnabled))
                    OnPropertyChanged(nameof(NotificationsEnabled));
                else if (e.PropertyName == nameof(TrayMenuViewModel.SendMediaPlayOnConnect))
                    OnPropertyChanged(nameof(SendMediaPlayOnConnect));
                else if (e.PropertyName == nameof(TrayMenuViewModel.SendMediaPauseOnDisconnect))
                    OnPropertyChanged(nameof(SendMediaPauseOnDisconnect));
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
