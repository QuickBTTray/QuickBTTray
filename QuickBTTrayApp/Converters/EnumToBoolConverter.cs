using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickBTTrayApp.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == parameter?.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Enum.Parse(targetType, parameter!.ToString()!) : Binding.DoNothing;
    }
}
