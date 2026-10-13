using PdfPixel.Color;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Converts a <see cref="PdfColor"/> into a brush, or leaves the target unset when there is no color.
/// </summary>
public class PdfColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PdfColor color)
        {
            return DependencyProperty.UnsetValue;
        }

        System.Windows.Media.Color brushColor = System.Windows.Media.Color.FromArgb(
            (byte)Math.Round(Math.Clamp(color.Alpha, 0f, 1f) * 255),
            (byte)Math.Round(Math.Clamp(color.Red, 0f, 1f) * 255),
            (byte)Math.Round(Math.Clamp(color.Green, 0f, 1f) * 255),
            (byte)Math.Round(Math.Clamp(color.Blue, 0f, 1f) * 255));

        SolidColorBrush brush = new(brushColor);
        brush.Freeze();

        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
