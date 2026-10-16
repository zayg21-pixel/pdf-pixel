using PdfPixel.Color;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf.Converters;

/// <summary>
/// Converts a <see cref="PdfColor"/> into a frozen <see cref="SolidColorBrush"/>, or leaves the target unset when there is no color.
/// </summary>
public sealed class PdfColorBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PdfColor color)
        {
            return DependencyProperty.UnsetValue;
        }

        System.Windows.Media.Color brushColor = System.Windows.Media.Color.FromArgb(
            ToByte(color.Alpha),
            ToByte(color.Red),
            ToByte(color.Green),
            ToByte(color.Blue));

        SolidColorBrush brush = new(brushColor);
        brush.Freeze();

        return brush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static byte ToByte(float component)
    {
        if (component <= 0f)
        {
            return 0;
        }

        if (component >= 1f)
        {
            return 255;
        }

        return (byte)Math.Round(component * 255f);
    }
}
