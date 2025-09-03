using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EAABAddIn.Data
{
    public class PostgresVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string motor = value as string;
            return (motor == "PostgreSQL") ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
