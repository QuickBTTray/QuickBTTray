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

        public ICommand ToggleRunOnStartupCommand { get; }
        public ICommand OpenBluetoothSettingsCommand => _trayMenuViewModel.OpenBluetoothSettingsCommand;

        public bool ConnectByUI
        {
            get => _trayMenuViewModel.ConnectByUI;
            set
            {
                if (_trayMenuViewModel.ConnectByUI == value) return;
                _trayMenuViewModel.ConnectByUI = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectByAPI));
            }
        }

        public bool ConnectByAPI
        {
            get => _trayMenuViewModel.ConnectByAPI;
            set
            {
                if (_trayMenuViewModel.ConnectByAPI == value) return;
                _trayMenuViewModel.ConnectByAPI = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectByUI));
            }
        }

        public bool DisconnectByUI
        {
            get => _trayMenuViewModel.DisconnectByUI;
            set
            {
                if (_trayMenuViewModel.DisconnectByUI == value) return;
                _trayMenuViewModel.DisconnectByUI = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisconnectByAPI));
            }
        }

        public bool DisconnectByAPI
        {
            get => _trayMenuViewModel.DisconnectByAPI;
            set
            {
                if (_trayMenuViewModel.DisconnectByAPI == value) return;
                _trayMenuViewModel.DisconnectByAPI = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisconnectByUI));
            }
        }

        public SettingsViewModel(StartupService startupService, TrayMenuViewModel trayMenuViewModel)
        {
            _startupService = startupService;
            _trayMenuViewModel = trayMenuViewModel;
            // Read live registry value — always reflects actual system state
            _runOnStartup = startupService.IsEnabled();
            ToggleRunOnStartupCommand = new RelayCommand(_ => RunOnStartup = !RunOnStartup);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
