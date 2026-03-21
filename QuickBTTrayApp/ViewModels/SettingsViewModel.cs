using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuickBTTrayApp.Services;

namespace QuickBTTrayApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly StartupService _startupService;
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

        public SettingsViewModel(StartupService startupService)
        {
            _startupService = startupService;
            // Read live registry value — always reflects actual system state
            _runOnStartup = startupService.IsEnabled();
            ToggleRunOnStartupCommand = new RelayCommand(_ => RunOnStartup = !RunOnStartup);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
