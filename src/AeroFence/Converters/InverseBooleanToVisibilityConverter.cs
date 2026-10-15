using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AeroFence.Converters;

/// <summary>
/// Converts boolean true to Collapsed and false to Visible.
/// Useful for toggling between display view and inline edit mode.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility vis && vis != Visibility.Visible;
    }
}
