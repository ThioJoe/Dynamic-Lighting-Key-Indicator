using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamic_Lighting_Key_Indicator.Converters
{
    public partial class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Windows.UI.Color color)
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
}