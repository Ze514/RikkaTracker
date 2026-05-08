using CommunityToolkit.Mvvm.ComponentModel;

namespace RikkaTracker.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _welcomeMessage = "概览数据正在统计中...";
    }
}
