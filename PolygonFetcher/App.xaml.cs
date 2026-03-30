using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PolygonFetcher
{
    public partial class App : Application
    {
    }

    public class LogColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string message)
            {
                if (message.Contains("ERROR"))
                    return new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                if (message.Contains("No data") || message.Contains("cancelled") || message.Contains("Warning"))
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
                if (message.Contains("bars fetched"))
                    return new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x4E));
            }
            return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
