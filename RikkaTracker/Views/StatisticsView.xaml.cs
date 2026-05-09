using System.Windows.Controls;

namespace RikkaTracker.Views
{
    public partial class StatisticsView : UserControl
    {
        public StatisticsView()
        {
            InitializeComponent();
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
