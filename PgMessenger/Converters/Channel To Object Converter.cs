using PgMessenger;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Converters
{
    [ValueConversion(typeof(ChannelType), typeof(object))]
    public class ChannelToObjectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ChannelType ChannelValue = (ChannelType)value;
            int IntValue = (int)ChannelValue;
            CompositeCollection CollectionOfItems = parameter as CompositeCollection;

            if (IntValue >= CollectionOfItems.Count)
                IntValue = 0;

            return CollectionOfItems[IntValue];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
