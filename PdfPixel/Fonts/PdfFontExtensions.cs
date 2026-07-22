using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using System;

namespace PdfPixel.Fonts;

/// <summary>
/// Conversions between core PDF model types and their <c>PdfPixel.Fonts</c> counterparts.
/// </summary>
public static class PdfFontExtensions
{
    /// <summary>
    /// Converts a <see cref="PdfFontString"/> to a <see cref="PdfString"/>.
    /// </summary>
    public static PdfString ToPdfString(this in PdfFontString value) => new(value.Value);

    /// <summary>
    /// Converts a <see cref="PdfString"/> to a <see cref="PdfFontString"/>.
    /// </summary>
    public static PdfFontString ToPdfFontString(this in PdfString value) => new(value.Value);

    /// <summary>
    /// Converts a <see cref="PdfFontEncoding"/> to a <see cref="PdfEncoding"/>.
    /// </summary>
    public static PdfEncoding ToPdfEncoding(this PdfFontEncoding encoding)
    {
        return encoding switch
        {
            PdfFontEncoding.StandardEncoding => PdfEncoding.StandardEncoding,
            PdfFontEncoding.MacRomanEncoding => PdfEncoding.MacRomanEncoding,
            PdfFontEncoding.WinAnsiEncoding => PdfEncoding.WinAnsiEncoding,
            PdfFontEncoding.MacExpertEncoding => PdfEncoding.MacExpertEncoding,
            PdfFontEncoding.SymbolEncoding => PdfEncoding.SymbolEncoding,
            PdfFontEncoding.ZapfDingbatsEncoding => PdfEncoding.ZapfDingbatsEncoding,
            _ => PdfEncoding.Unknown
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfEncoding"/> to a <see cref="PdfFontEncoding"/>.
    /// </summary>
    public static PdfFontEncoding ToPdfFontEncoding(this PdfEncoding encoding)
    {
        return encoding switch
        {
            PdfEncoding.StandardEncoding => PdfFontEncoding.StandardEncoding,
            PdfEncoding.MacRomanEncoding => PdfFontEncoding.MacRomanEncoding,
            PdfEncoding.WinAnsiEncoding => PdfFontEncoding.WinAnsiEncoding,
            PdfEncoding.MacExpertEncoding => PdfFontEncoding.MacExpertEncoding,
            PdfEncoding.SymbolEncoding => PdfFontEncoding.SymbolEncoding,
            PdfEncoding.ZapfDingbatsEncoding => PdfFontEncoding.ZapfDingbatsEncoding,
            _ => PdfFontEncoding.Unknown
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfStandardFontName"/> to a <see cref="PdfFontStandardName"/>.
    /// </summary>
    public static PdfFontStandardName ToPdfFontStandardName(this PdfStandardFontName fontName)
    {
        return fontName switch
        {
            PdfStandardFontName.Times => PdfFontStandardName.Times,
            PdfStandardFontName.TimesNewRoman => PdfFontStandardName.TimesNewRoman,
            PdfStandardFontName.TimesNewRomanPS => PdfFontStandardName.TimesNewRomanPS,
            PdfStandardFontName.Helvetica => PdfFontStandardName.Helvetica,
            PdfStandardFontName.Arial => PdfFontStandardName.Arial,
            PdfStandardFontName.Courier => PdfFontStandardName.Courier,
            PdfStandardFontName.CourierNew => PdfFontStandardName.CourierNew,
            PdfStandardFontName.CourierNewPS => PdfFontStandardName.CourierNewPS,
            PdfStandardFontName.Symbol => PdfFontStandardName.Symbol,
            PdfStandardFontName.ZapfDingbats => PdfFontStandardName.ZapfDingbats,
            _ => throw new ArgumentOutOfRangeException(nameof(fontName), fontName, message: null)
        };
    }

    /// <summary>
    /// Converts a <see cref="PdfFontDescriptor"/> to a <see cref="PdfFontMetrics"/>.
    /// </summary>
    public static PdfFontMetrics ToPdfFontMetrics(this PdfFontDescriptor descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        return new PdfFontMetrics
        {
            FontName = descriptor.FontName.ToPdfFontString(),
            Ascent = descriptor.Ascent,
            Descent = descriptor.Descent,
            CapHeight = descriptor.CapHeight,
            XHeight = descriptor.XHeight,
            ItalicAngle = descriptor.ItalicAngle,
            BoundingBoxLeft = descriptor.FontBBox.Left,
            BoundingBoxBottom = descriptor.FontBBox.Bottom,
            BoundingBoxRight = descriptor.FontBBox.Right,
            BoundingBoxTop = descriptor.FontBBox.Top,
            AvgWidth = descriptor.AvgWidth,
            Weight = descriptor.FontWeight,
            IsForceBold = (descriptor.Flags & PdfFontFlags.ForceBold) != 0,
            IsItalic = (descriptor.Flags & PdfFontFlags.Italic) != 0,
            Panose = descriptor.Panose
        };
    }
}
