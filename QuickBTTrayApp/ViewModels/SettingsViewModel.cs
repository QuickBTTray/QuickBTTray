using System.ComponentModel;
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

        public ICommand ToggleRunOnStartupCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
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

            _trayMenuViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrayMenuViewModel.ConnectBy))
                    OnPropertyChanged(nameof(ConnectBy));
                else if (e.PropertyName == nameof(TrayMenuViewModel.DisconnectBy))
                    OnPropertyChanged(nameof(DisconnectBy));
                else if (e.PropertyName == nameof(TrayMenuViewModel.NotificationsEnabled))
                    OnPropertyChanged(nameof(NotificationsEnabled));
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
