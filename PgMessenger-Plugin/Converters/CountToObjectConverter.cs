using System;
using System.Globalization;
using System.Windows.Data;

namespace Converters
{
    [ValueConversion(typeof(int), typeof(object))]
    public class CountToObjectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int IntValue = (int)value;
            CompositeCollection CollectionOfItems = parameter as CompositeCollection;

            return CollectionOfItems[IntValue > 0 ? 1 : 0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
