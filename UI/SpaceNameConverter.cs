using System.Globalization;
using System.Windows.Data;
using System;

namespace Commons
{
    public class SpaceNameConverter : IValueConverter
    {
        public object Convert(Object value, Type targetType, object parameter, CultureInfo culture)
        {
            string spaceName = (string)value;
            spaceName = spaceName.ToUpper();
            return spaceName[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return "";
        }
    }
}