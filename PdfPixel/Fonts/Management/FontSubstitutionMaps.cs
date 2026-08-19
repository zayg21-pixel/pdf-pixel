using PdfPixel.Fonts.Mapping;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// The built-in <see cref="FontSubstitutionMap"/> instances: one per platform, naming the font
/// families that platform ships to stand in for the Standard 14, plus the canonical PDF names used
/// with an explicitly registered font source.
/// </summary>
public static class FontSubstitutionMaps
{
    /// <summary>
    /// Families installed by Windows, resolved through DirectWrite.
    /// </summary>
    public static FontSubstitutionMap Windows { get; } = new(
        new Dictionary<PdfStandardFontName, IReadOnlyList<string>>
        {
            { PdfStandardFontName.Times, ["Times New Roman"] },
            { PdfStandardFontName.TimesNewRoman, ["Times New Roman"] },
            { PdfStandardFontName.TimesNewRomanPS, ["Times New Roman"] },
            { PdfStandardFontName.Helvetica, ["Arial"] },
            { PdfStandardFontName.Arial, ["Arial"] },
            { PdfStandardFontName.Courier, ["Courier New"] },
            { PdfStandardFontName.CourierNew, ["Courier New"] },
            { PdfStandardFontName.CourierNewPS, ["Courier New"] },
            { PdfStandardFontName.Symbol, ["Segoe UI Symbol", "Times New Roman"] },
            { PdfStandardFontName.ZapfDingbats, ["Segoe UI Symbol"] }
        },
        "Arial");

    /// <summary>
    /// Metric-compatible families commonly installed on Linux, resolved through fontconfig. Liberation
    /// is preferred over DejaVu and URW because it matches the Standard 14 metrics.
    /// </summary>
    public static FontSubstitutionMap Linux { get; } = new(
        new Dictionary<PdfStandardFontName, IReadOnlyList<string>>
        {
            { PdfStandardFontName.Times, ["Liberation Serif", "DejaVu Serif", "Nimbus Roman"] },
            { PdfStandardFontName.TimesNewRoman, ["Liberation Serif", "DejaVu Serif", "Nimbus Roman"] },
            { PdfStandardFontName.TimesNewRomanPS, ["Liberation Serif", "DejaVu Serif", "Nimbus Roman"] },
            { PdfStandardFontName.Helvetica, ["Liberation Sans", "DejaVu Sans", "Nimbus Sans"] },
            { PdfStandardFontName.Arial, ["Liberation Sans", "DejaVu Sans", "Nimbus Sans"] },
            { PdfStandardFontName.Courier, ["Liberation Mono", "DejaVu Sans Mono", "Nimbus Mono PS"] },
            { PdfStandardFontName.CourierNew, ["Liberation Mono", "DejaVu Sans Mono", "Nimbus Mono PS"] },
            { PdfStandardFontName.CourierNewPS, ["Liberation Mono", "DejaVu Sans Mono", "Nimbus Mono PS"] },
            { PdfStandardFontName.Symbol, ["Standard Symbols PS", "OpenSymbol", "DejaVu Sans"] },
            { PdfStandardFontName.ZapfDingbats, ["D050000L", "Dingbats", "OpenSymbol"] }
        },
        "DejaVu Sans");

    /// <summary>
    /// Families installed by macOS, resolved through CoreText.
    /// </summary>
    public static FontSubstitutionMap MacOs { get; } = new(
        new Dictionary<PdfStandardFontName, IReadOnlyList<string>>
        {
            { PdfStandardFontName.Times, ["Times New Roman", "Times"] },
            { PdfStandardFontName.TimesNewRoman, ["Times New Roman", "Times"] },
            { PdfStandardFontName.TimesNewRomanPS, ["Times New Roman", "Times"] },
            { PdfStandardFontName.Helvetica, ["Helvetica", "Helvetica Neue"] },
            { PdfStandardFontName.Arial, ["Arial", "Helvetica"] },
            { PdfStandardFontName.Courier, ["Courier New", "Courier"] },
            { PdfStandardFontName.CourierNew, ["Courier New", "Courier"] },
            { PdfStandardFontName.CourierNewPS, ["Courier New", "Courier"] },
            { PdfStandardFontName.Symbol, ["Symbol", "Apple Symbols"] },
            { PdfStandardFontName.ZapfDingbats, ["Zapf Dingbats", "Apple Symbols"] }
        },
        "Helvetica");

    /// <summary>
    /// The families a browser resolves without a downloaded font file, as used by CSS font stacks.
    /// </summary>
    public static FontSubstitutionMap Browser { get; } = new(
        new Dictionary<PdfStandardFontName, IReadOnlyList<string>>
        {
            { PdfStandardFontName.Times, ["Times New Roman", "Times", "serif"] },
            { PdfStandardFontName.TimesNewRoman, ["Times New Roman", "Times", "serif"] },
            { PdfStandardFontName.TimesNewRomanPS, ["Times New Roman", "Times", "serif"] },
            { PdfStandardFontName.Helvetica, ["Helvetica", "Arial", "sans-serif"] },
            { PdfStandardFontName.Arial, ["Arial", "Helvetica", "sans-serif"] },
            { PdfStandardFontName.Courier, ["Courier New", "Courier", "monospace"] },
            { PdfStandardFontName.CourierNew, ["Courier New", "Courier", "monospace"] },
            { PdfStandardFontName.CourierNewPS, ["Courier New", "Courier", "monospace"] },
            { PdfStandardFontName.Symbol, ["Symbol", "Segoe UI Symbol", "serif"] },
            { PdfStandardFontName.ZapfDingbats, ["ZapfDingbats", "Segoe UI Symbol", "sans-serif"] }
        },
        "sans-serif");

    /// <summary>
    /// The names PDF documents themselves use for the Standard 14 fonts, for use with a font source
    /// that resolves only the names it was explicitly given. Carries no fallback family name, so the
    /// font source's own last-resort typeface is used directly.
    /// </summary>
    public static FontSubstitutionMap CanonicalNames { get; } = new(
        new Dictionary<PdfStandardFontName, IReadOnlyList<string>>
        {
            { PdfStandardFontName.Times, ["Times"] },
            { PdfStandardFontName.TimesNewRoman, ["Times New Roman", "TimesNewRomanPSMT"] },
            { PdfStandardFontName.TimesNewRomanPS, ["TimesNewRomanPS", "TimesNewRomanPSMT"] },
            { PdfStandardFontName.Helvetica, ["Helvetica"] },
            { PdfStandardFontName.Arial, ["Arial"] },
            { PdfStandardFontName.Courier, ["Courier"] },
            { PdfStandardFontName.CourierNew, ["Courier New"] },
            { PdfStandardFontName.CourierNewPS, ["CourierNewPS", "CourierNewPSMT"] },
            { PdfStandardFontName.Symbol, ["Symbol"] },
            { PdfStandardFontName.ZapfDingbats, ["ZapfDingbats"] }
        },
        null);

    /// <summary>
    /// The map matching the operating system this process is running on.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when the process is running on an operating system no built-in map covers. A map built
    /// for that platform has to be supplied to <see cref="FontProvider"/> directly.
    /// </exception>
    public static FontSubstitutionMap Current => GetCurrentPlatformMap();

    private static FontSubstitutionMap GetCurrentPlatformMap()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
        {
            return Browser;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return MacOs;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Linux;
        }

        throw new PlatformNotSupportedException($"No built-in font substitution map for '{RuntimeInformation.OSDescription}'.");
    }
}
