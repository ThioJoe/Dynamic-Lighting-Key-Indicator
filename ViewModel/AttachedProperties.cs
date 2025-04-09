using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamic_Lighting_Key_Indicator;
public static class ButtonParameters
{
    public static readonly DependencyProperty KeyNameProperty = // Property names usually end with "Property" by convention
        DependencyProperty.RegisterAttached(
            "KeyName", // Registered name (used in XAML)
            typeof(VKey),
            typeof(ButtonParameters),
            new PropertyMetadata(VKey.Null));

    public static void SetKeyName(DependencyObject element, VKey value)
    {
        element.SetValue(KeyNameProperty, value);
    }
    public static VKey GetKeyName(DependencyObject element)
    {
        if (element.GetValue(KeyNameProperty) is VKey vk)
            return vk;
        else
            return VKey.Null; // Return a default value if not set
    }

    // Define second parameter property
    public static readonly DependencyProperty ColorStateProperty = // Property names usually end with "Property" by convention
        DependencyProperty.RegisterAttached(
            "ColorState", // Registered name (used in XAML)
            typeof(StateColorApply),
            typeof(ButtonParameters),
            new PropertyMetadata(StateColorApply.Null));

    public static void SetColorState(DependencyObject element, StateColorApply value)
    {
        element.SetValue(ColorStateProperty, value);
    }
    public static StateColorApply GetColorState(DependencyObject element)
    {
        return (StateColorApply)element.GetValue(ColorStateProperty);
    }
}
