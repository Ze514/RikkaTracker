using CommunityToolkit.Mvvm.ComponentModel;

namespace RikkaTrack.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _welcomeMessage = "概览数据正在统计中...";
    }
}
