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
                return string.Equals(stringValue, parameter.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (value is bool boolValue)
            {
                if (parameter != null && parameter.ToString().Equals("Invert", StringComparison.OrdinalIgnoreCase))
                {
                    return !boolValue ? Visibility.Visible : Visibility.Collapsed;
                }
                
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                if (parameter != null && parameter.ToString().Equals("Invert", StringComparison.OrdinalIgnoreCase))
                {
                    return visibility != Visibility.Visible;
                }
                
                return visibility == Visibility.Visible;
            }

            return false;
        }
    }

    /// <summary>
    /// Converter especializado para habilitar/deshabilitar controles
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
                
            return true; // Por defecto habilitado
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
                
            return false;
        }
    }
}