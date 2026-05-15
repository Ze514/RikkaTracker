using System.Windows.Controls;

namespace RikkaTracker.Views
{
    public partial class StatisticsView : UserControl
    {
        public StatisticsView()
        {
            InitializeComponent();
            this.DataContextChanged += StatisticsView_DataContextChanged;
            this.Loaded += (s, e) => 
            {
                // 页面加载时尝试滚动一次
                Dispatcher.BeginInvoke(new Action(() => ScrollToLatest()), System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        private void StatisticsView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.StatisticsViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is ViewModels.StatisticsViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.StatisticsViewModel.RefreshTrigger))
            {
                // 只有当显式触发刷新（加载数据或点击追踪）时，才滚动到最新记录
                Dispatcher.BeginInvoke(new Action(() => 
                {
                    ScrollToLatest();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ScrollToLatest()
        {
            if (TimelineScroller == null || DataContext is not ViewModels.StatisticsViewModel vm) return;
            if (vm.GanttSegments == null || !vm.GanttSegments.Any()) return;

            // 强制布局更新以确保 ViewportWidth 准确
            TimelineScroller.UpdateLayout();

            var segments = vm.GanttSegments.ToList();
            var latestEnd = segments.Max(s => s.End);
            var baseTime = segments.First().Start.Date;
            var pixelsPerHour = ZoomSlider.Value;

            double x = (latestEnd - baseTime).TotalHours * pixelsPerHour;
            
            // 将最新记录定位在视图中间
            double targetOffset = x - (TimelineScroller.ViewportWidth / 2);
            if (targetOffset < 0) targetOffset = 0;
            
            TimelineScroller.ScrollToHorizontalOffset(targetOffset);
        }

        private void TimelineScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                HeaderScroller.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }

        private void TimelineScroller_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // 如果按住了 Shift，或者按照用户需求默认就进行水平滚动
            TimelineScroller.ScrollToHorizontalOffset(TimelineScroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
