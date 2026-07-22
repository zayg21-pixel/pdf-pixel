using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// Builds the right <see cref="IPdfTypeface"/> implementation for a font program's outline format.
/// </summary>
public static class PdfTypefaceFactory
{
    /// <summary>
    /// Parses and repacks a font program into an <see cref="IPdfTypeface"/>.
    /// </summary>
    /// <param name="fontBytes">The raw font program bytes: a bare CFF font for
    /// <see cref="PdfTypefaceType.Cff"/>, or an SFNT font for <see cref="PdfTypefaceType.TrueType"/>.</param>
    /// <param name="type">Which outline format <paramref name="fontBytes"/> is in.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public static IPdfTypeface Create(in ReadOnlyMemory<byte> fontBytes, PdfTypefaceType type, ILoggerFactory loggerFactory)
    {
        return type switch
        {
            PdfTypefaceType.Cff => new CffPdfTypeface(fontBytes, loggerFactory),
            PdfTypefaceType.TrueType => new TrueTypePdfTypeface(fontBytes, loggerFactory),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown typeface type.")
        };
    }
}
