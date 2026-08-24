using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Fonts.Resources;

/// <summary>
/// Serves the Standard 14 font programs this assembly embeds. Each one is parsed the first time it is
/// asked for and held for the lifetime of the process, shared by every caller that resolves to it, and
/// never disposed.
/// </summary>
public static class Standard14TypefaceLoader
{
    private const string ResourceDirectory = "Standard14Fonts";

    private static readonly Dictionary<PdfFontStandardName, Standard14FontFiles> FontFilesByName = new()
    {
        [PdfFontStandardName.Times] = new Standard14FontFiles("NimbusRoman-Regular.otf", "NimbusRoman-Bold.otf", "NimbusRoman-Italic.otf", "NimbusRoman-BoldItalic.otf"),
        [PdfFontStandardName.TimesNewRoman] = new Standard14FontFiles("NimbusRoman-Regular.otf", "NimbusRoman-Bold.otf", "NimbusRoman-Italic.otf", "NimbusRoman-BoldItalic.otf"),
        [PdfFontStandardName.TimesNewRomanPS] = new Standard14FontFiles("NimbusRoman-Regular.otf", "NimbusRoman-Bold.otf", "NimbusRoman-Italic.otf", "NimbusRoman-BoldItalic.otf"),
        [PdfFontStandardName.Helvetica] = new Standard14FontFiles("NimbusSans-Regular.otf", "NimbusSans-Bold.otf", "NimbusSans-Oblique.otf", "NimbusSans-BoldOblique.otf"),
        [PdfFontStandardName.Arial] = new Standard14FontFiles("NimbusSans-Regular.otf", "NimbusSans-Bold.otf", "NimbusSans-Oblique.otf", "NimbusSans-BoldOblique.otf"),
        [PdfFontStandardName.Courier] = new Standard14FontFiles("NimbusMonoPS-Regular.otf", "NimbusMonoPS-Bold.otf", "NimbusMonoPS-Italic.otf", "NimbusMonoPS-BoldItalic.otf"),
        [PdfFontStandardName.CourierNew] = new Standard14FontFiles("NimbusMonoPS-Regular.otf", "NimbusMonoPS-Bold.otf", "NimbusMonoPS-Italic.otf", "NimbusMonoPS-BoldItalic.otf"),
        [PdfFontStandardName.CourierNewPS] = new Standard14FontFiles("NimbusMonoPS-Regular.otf", "NimbusMonoPS-Bold.otf", "NimbusMonoPS-Italic.otf", "NimbusMonoPS-BoldItalic.otf"),
        [PdfFontStandardName.Symbol] = new Standard14FontFiles("StandardSymbolsPS.otf", "StandardSymbolsPS.otf", "StandardSymbolsPS.otf", "StandardSymbolsPS.otf"),
        [PdfFontStandardName.ZapfDingbats] = new Standard14FontFiles("D050000L.otf", "D050000L.otf", "D050000L.otf", "D050000L.otf")
    };

    // Keyed by resource file name, so the families that share one file - Times and Times New Roman,
    // Helvetica and Arial - read one parse between them.
    private static readonly ConcurrentDictionary<string, Lazy<SfntPdfTypeface>> Typefaces = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the typeface embedded for a Standard 14 family in the requested style. The result is
    /// shared by every caller and must not be disposed.
    /// </summary>
    /// <param name="fontName">The Standard 14 font family.</param>
    /// <param name="bold">Whether to resolve the bold style variant.</param>
    /// <param name="italic">Whether to resolve the italic/oblique style variant.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontName"/> is not a Standard 14 family.</exception>
    public static SfntPdfTypeface GetTypeface(PdfFontStandardName fontName, bool bold, bool italic, ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        if (!FontFilesByName.TryGetValue(fontName, out Standard14FontFiles fontFiles))
        {
            throw new ArgumentOutOfRangeException(nameof(fontName), fontName, message: null);
        }

        string fileName = fontFiles.GetFileName(bold, italic);

        return Typefaces.GetOrAdd(fileName, name => new Lazy<SfntPdfTypeface>(() => LoadTypeface(name, loggerFactory))).Value;
    }

    /// <summary>
    /// Parses one Standard 14 resource font. The program is served to a rasterizer as it stands.
    /// </summary>
    private static SfntPdfTypeface LoadTypeface(string fileName, ILoggerFactory loggerFactory)
    {
        byte[] fontProgram = FontResourceLoader.GetResource($"{ResourceDirectory}.{fileName}");
        SfntPdfTypefaceParameters parameters = new() { RepackTypeface = false };

        return new SfntPdfTypeface(new MemoryStream(fontProgram, writable: false), loggerFactory, parameters);
    }

    /// <summary>
    /// The resource file names one Standard 14 family's four styles are held in.
    /// </summary>
    private readonly struct Standard14FontFiles
    {
        public Standard14FontFiles(string regular, string bold, string italic, string boldItalic)
        {
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
        }

        public string Regular { get; }

        public string Bold { get; }

        public string Italic { get; }

        public string BoldItalic { get; }

        public string GetFileName(bool bold, bool italic)
        {
            if (bold)
            {
                return italic ? BoldItalic : Bold;
            }

            return italic ? Italic : Regular;
        }
    }
}
