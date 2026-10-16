using PdfPixel.Demo.Wpf.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

namespace PdfPixel.Demo.Wpf.Converters;

/// <summary>
/// Converts a <see cref="DocumentSidePanel"/> into whether it equals the parameter, and a checked state back
/// into the parameter, or <see cref="DocumentSidePanel.None"/> when unchecked.
/// </summary>
public class SidePanelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Equals(value, parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : DocumentSidePanel.None;
}
