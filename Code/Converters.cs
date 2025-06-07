namespace MediFiler_V2.Code;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

public class BoolToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool isLast && isLast) ? 30 : 20; // Adjust heights as needed
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public class BoolToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool isLast && isLast) ? new Thickness(0, 0, 0, 1) : new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public class BottomPaddingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool isLast && isLast)
            ? new Thickness(0, 0, 0, 12) // Add 10px padding at bottom
            : new Thickness(0);          // No extra padding
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
