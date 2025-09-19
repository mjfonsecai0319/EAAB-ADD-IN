using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EAABAddIn.Src.Presentation.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && value is string stringValue)
            {
                // Si se pasa un parámetro (ej: "Oracle"), solo muestra cuando coincide
                return string.Equals(stringValue, parameter.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;

            return false;
        }
    }
}
