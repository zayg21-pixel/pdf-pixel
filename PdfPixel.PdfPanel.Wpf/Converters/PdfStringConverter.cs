using PdfPixel.Objects;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdfPixel.PdfPanel.Wpf.Converters;

/// <summary>
/// Converts a <see cref="PdfString"/> into its decoded text, or leaves the target unset when there is no string.
/// </summary>
public sealed class PdfStringConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PdfString text)
        {
            return DependencyProperty.UnsetValue;
        }

        return text.DecodePdfString();
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
