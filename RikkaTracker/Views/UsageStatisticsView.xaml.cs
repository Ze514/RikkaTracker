using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections.Generic;
using System.Linq;
using RikkaTracker.ViewModels;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.WPF;

namespace RikkaTracker.Views
{
    public partial class UsageStatisticsView : UserControl
    {
        public UsageStatisticsView()
        {
            InitializeComponent();
        }

        private void CartesianChart_DataPointerDown(IChartView chart, IEnumerable<ChartPoint> points)
        {
            var point = points.FirstOrDefault();
            if (point != null && DataContext is UsageStatisticsViewModel vm)
            {
                if (vm.ChartPointClickedCommand.CanExecute(point))
                {
                    vm.ChartPointClickedCommand.Execute(point);
                }
            }
        }
    }

    public class EnumMatchToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            string? checkValue = value.ToString();
            string? targetValue = parameter.ToString();
            if (checkValue == null || targetValue == null) return false;
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Binding.DoNothing;
            bool useValue = (bool)value;
            if (useValue)
            {
                string? pStr = parameter.ToString();
                if (pStr != null) return Enum.Parse(targetType, pStr);
            }
            return Binding.DoNothing;
        }
    }

    public class EnumMatchToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            string? checkValue = value.ToString();
            string? targetValue = parameter.ToString();
            if (checkValue == null || targetValue == null) return Visibility.Collapsed;
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
