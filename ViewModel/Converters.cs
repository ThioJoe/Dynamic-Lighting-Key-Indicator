using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;

namespace Dynamic_Lighting_Key_Indicator.Converters
{
    public partial class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            return null;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return null;
        }
    }

    public partial class DllPathToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            string propertyName = "System.Devices.Icon"; // Could use "System.Devices.Icon" or "System.Devices.GlyphIcon"

            if (value is IReadOnlyDictionary<string, object> dict
                && dict.TryGetValue(propertyName, out object? iconValue) // Either
                && iconValue is string dllRawPath
                && !string.IsNullOrEmpty(dllRawPath))
            {
                BitmapImage image = MainWindow.LoadSystemDllIcon(dllRawPath);
                return image;
            }
            return null;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }

}