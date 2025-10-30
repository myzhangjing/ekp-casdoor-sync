using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncEkpToCasdoor.UI;

/// <summary>
/// 当绑定值等于参数时返回 Visible，否则返回 Collapsed。
/// </summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString() ?? string.Empty;
        var p = parameter?.ToString() ?? string.Empty;
        return string.Equals(s, p, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
