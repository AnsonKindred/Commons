using System.Globalization;
using System.Windows.Data;
using System;

namespace Commons
{
    public class ServerNameConverter : IValueConverter
    {
        public object Convert(Object value, Type targetType, object parameter, CultureInfo culture)
        {
            string serverName = (string)value;
            serverName = serverName.ToUpper();
            return serverName[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return "";
        }
    }
}