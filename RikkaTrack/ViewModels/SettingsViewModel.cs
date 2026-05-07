using CommunityToolkit.Mvvm.ComponentModel;

namespace RikkaTrack.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isAutoStart = true;

        [ObservableProperty]
        private string _storagePath = "Default";
    }
}
