using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AeroFence.Services.Native;

namespace AeroFence.Converters;

/// <summary>
/// Converts an application or file target path into a native WPF ImageSource icon.
/// </summary>
public class PathToIconConverter : IValueConverter
{
    private static readonly ShellIconService _iconService = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.ItemModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.CustomIconPath))
            {
                var customIcon = _iconService.GetIcon(item.CustomIconPath);
                if (customIcon != null) return customIcon;
            }
            return _iconService.GetIcon(item.TargetPath);
        }

        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            return _iconService.GetIcon(path);
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw freshNotSupportedException();

        static NotSupportedException freshNotSupportedException() => new();
    }
}
