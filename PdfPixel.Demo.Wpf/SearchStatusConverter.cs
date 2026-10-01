using PdfPixel.PdfPanel.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Formats the search status: "Searching..." until every page's text is extracted, then "Not found",
/// "current of count", or "count found" when no result is current.
/// </summary>
public class SearchStatusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[4] is not string query || query.Length == 0)
        {
            return string.Empty;
        }

        if (values[3] is not true)
        {
            return "Searching...";
        }

        if (values[0] is not IList<PdfPanelSearchMatch> results || results.Count == 0)
        {
            return "Not found";
        }

        if (values[1] is PdfPanelSearchMatch currentResult)
        {
            int currentIndex = results.IndexOf(currentResult);

            if (currentIndex >= 0)
            {
                return $"{currentIndex + 1} of {results.Count}";
            }
        }

        return $"{results.Count} found";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
