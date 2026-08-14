using System;
using System.Globalization;
using PdfPixel.Models;

namespace PdfPixel.Parsing;

/// <summary>
/// Utility class for parsing PDF dates from PdfString values.
/// </summary>
internal static class PdfDateParser
{
    /// <summary>
    /// Parses a PDF date string into a DateTime object.
    /// </summary>
    /// <param name="pdfString">The PDF string containing the date.</param>
    /// <returns>The parsed DateTime, or null if parsing fails or the string is absent or empty.</returns>
    /// <remarks>
    /// <para>
    /// PDF dates follow the format: D:YYYYMMDDHHmmSSOHH'mm
    /// Where:
    /// - YYYY is the year
    /// - MM is the month (01-12)
    /// - DD is the day (01-31)
    /// - HH is the hour (00-23)
    /// - mm is the minute (00-59)
    /// - SS is the second (00-59)
    /// - O is '+', '-', or 'Z' for timezone offset
    /// - HH'mm is the timezone offset hours and minutes
    /// </para>
    /// <para>Trailing components may be omitted, e.g., D:2023 or D:20231225</para>
    /// </remarks>
    public static DateTime? ParsePdfDate(PdfString? pdfString)
    {
        if (pdfString == null || pdfString.Value.IsEmpty)
        {
            return null;
        }

        string dateString = pdfString.Value.ToString();

        // Must start with "D:"
        if (!dateString.StartsWith("D:"))
        {
            return null;
        }

        string dateContent = dateString.Substring(2); // Remove "D:" prefix

        // Pad with defaults for missing components: D:YYYY -> D:YYYY0101000000
        string paddedDate = dateContent.PadRight(14, '0');

        // If we have timezone info, preserve it
        string timezonePart = string.Empty;
        if (dateContent.Length > 14)
        {
            timezonePart = dateContent.Substring(14);
        }

        // Convert PDF timezone format +05'30 to standard +0530
        if (timezonePart.Length >= 6 && timezonePart.Contains("'"))
        {
            timezonePart = timezonePart.Replace("'", string.Empty);
        }

        string fullDateString = paddedDate + timezonePart;

        // Try to parse with timezone first
        if (timezonePart.Length > 0)
        {
            string format = (timezonePart == "Z") ? "yyyyMMddHHmmss'Z'" : "yyyyMMddHHmmsszzz";
            if (DateTime.TryParseExact(fullDateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime resultWithTz))
            {
                return resultWithTz.ToUniversalTime();
            }
        }

        // Try to parse without timezone
        if (DateTime.TryParseExact(paddedDate, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }
}
