using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace EAABAddIn.Src.Presentation.Converters
{
    public class BoldLabelsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string text || string.IsNullOrEmpty(text))
                return value;

            // Crear un TextBlock con Run elements donde las etiquetas estén en negrita
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
            
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    textBlock.Inlines.Add(new LineBreak());
                    continue;
                }

                // Detectar si la línea contiene etiquetas que deben ser negrita
                var lowerLine = line.ToLowerInvariant();
                
                if (lowerLine.Contains("archivo:") || lowerLine.Contains("hash esperado:") || lowerLine.Contains("hash actual:"))
                {
                    // Buscar la posición del separador ":"
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        // Etiqueta en negrita
                        var run = new Run(line.Substring(0, colonIndex + 1)) { FontWeight = FontWeights.Bold };
                        textBlock.Inlines.Add(run);
                        // Valor normal
                        var valueRun = new Run(line.Substring(colonIndex + 1));
                        textBlock.Inlines.Add(valueRun);
                    }
                    else
                    {
                        textBlock.Inlines.Add(new Run(line));
                    }
                }
                else
                {
                    textBlock.Inlines.Add(new Run(line));
                }

                textBlock.Inlines.Add(new LineBreak());
            }

            return textBlock;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
