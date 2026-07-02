using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EMECore.WinUI.Converters;

public class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string page && parameter is string targetPage)
            return page == targetPage ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class PlayTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int minutes)
        {
            var ts = TimeSpan.FromMinutes(minutes);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }
        return "0m";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class PlatformIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString()?.ToLower() switch
        {
            "steam" => "\uE710",
            "epic" => "\uE74C",
            "xbox" => "\uE70E",
            "gog" => "\uE7C3",
            _ => "\uE74E"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
