using System;
using System.Globalization;
using System.Windows.Data;

namespace RikkaTracker.Converters
{
    public class MultiplyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return 0.0;
            
            double result = 1.0;
            foreach (var val in values)
            {
                if (val is double d) result *= d;
                else if (val is int i) result *= i;
                else if (val is float f) result *= f;
                else if (val is string s && double.TryParse(s, out double ds)) result *= ds;
            }
            
            // 确保最小高度，避免完全消失
            return Math.Max(result, 2.0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
