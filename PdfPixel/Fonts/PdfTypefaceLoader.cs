using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Type1;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Models;
using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfPixel.Fonts;

/// <summary>
/// Loads an <see cref="IPdfTypeface"/> from an embedded PDF font program - an sfnt font program
/// (TrueType, or CFF-flavored OpenType), a PostScript Type1 program, or a bare CFF program
/// (Type1C, CIDFontType0C). The layout is detected from the program's own bytes rather than taken
/// from the format the font descriptor declares, and every other layout is attempted in turn when
/// the detected one fails to parse.
/// </summary>
public sealed class PdfTypefaceLoader
{
    // Enough of the program's start to carry an sfnt version tag or a Type1 marker past any
    // leading whitespace, without pulling the body of the font into memory to decide.
    private const int LayoutProbeBytes = 64;

    private static readonly PdfFontProgramLayout[] AllLayouts =
    [
        PdfFontProgramLayout.Sfnt,
        PdfFontProgramLayout.Type1,
        PdfFontProgramLayout.Cff
    ];

    private readonly PdfFontDescriptor _fontDescriptor;
    private readonly bool _isComposite;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PdfTypefaceLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTypefaceLoader"/> class for the font program
    /// <paramref name="fontDescriptor"/> embeds, loaded on behalf of a font of the given PDF font
    /// subtype.
    /// </summary>
    /// <param name="fontDescriptor">The font descriptor holding the embedded font program.</param>
    /// <param name="fontSubType">The PDF font dictionary's /Subtype.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public PdfTypefaceLoader(PdfFontDescriptor fontDescriptor, PdfFontSubType fontSubType, ILoggerFactory loggerFactory)
    {
        if (fontDescriptor == null)
        {
            throw new ArgumentNullException(nameof(fontDescriptor));
        }

        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _fontDescriptor = fontDescriptor;
        _isComposite = fontSubType is PdfFontSubType.CidFontType0 or PdfFontSubType.CidFontType2;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfTypefaceLoader>();
    }

    /// <summary>
    /// Returns the typeface this loader's font descriptor embeds. The result is cached on the document
    /// by the font program object's reference, and parsed only on the first call for that object.
    /// </summary>
    /// <returns>The typeface the font program parses as.</returns>
    /// <exception cref="InvalidDataException">The font program does not parse as any known layout.</exception>
    public IPdfTypeface GetTypeface()
    {
        PdfDocumentObjectCache? objectCache = _fontDescriptor.ObjectCache;
        PdfReference fontFileReference = _fontDescriptor.FontFileReference;

        if (objectCache == null || !fontFileReference.IsValid)
        {
            return LoadTypeface();
        }

        if (objectCache.Typefaces.TryGetValue(fontFileReference, out IPdfTypeface? cachedTypeface))
        {
            return cachedTypeface;
        }

        IPdfTypeface typeface = LoadTypeface();
        objectCache.Typefaces[fontFileReference] = typeface;

        return typeface;
    }

    /// <summary>
    /// Parses the embedded font program. The declared format is used for nothing: the layout is detected
    /// from the leading bytes, and if that parse fails the remaining layouts are tried before giving up.
    /// </summary>
    private IPdfTypeface LoadTypeface()
    {
        Stream? decodedStream = _fontDescriptor.FontFileStream?.DecodeAsStream();
        if (decodedStream == null)
        {
            throw new InvalidDataException("The font descriptor embeds no font program.");
        }

        ReadOnlyFontStream fontStream = ReadOnlyFontStream.Create(decodedStream, leaveOpen: false);

        PdfFontProgramLayout detectedLayout = DetectLayout(fontStream);
        IPdfTypeface? typeface = TryLoad(detectedLayout, fontStream);

        if (typeface != null)
        {
            return typeface;
        }

        foreach (PdfFontProgramLayout fallbackLayout in AllLayouts)
        {
            if (fallbackLayout == detectedLayout)
            {
                continue;
            }

            typeface = TryLoad(fallbackLayout, fontStream);

            if (typeface != null)
            {
                _logger.LogWarning(
                    "Embedded font program declared /{DeclaredFormat} and detected as {DetectedLayout} parsed as {ActualLayout}.",
                    _fontDescriptor.FontFileFormat,
                    detectedLayout,
                    fallbackLayout);

                return typeface;
            }
        }

        fontStream.Dispose();

        throw new InvalidDataException("Data is not a valid font program.");
    }

    /// <summary>
    /// Attempts one layout against <paramref name="fontStream"/>, rewinding it first so a failed
    /// attempt hands the next one a stream positioned at the start. Ownership passes to the typeface
    /// this returns; a failed attempt leaves the stream open for the next layout to try.
    /// </summary>
    private IPdfTypeface? TryLoad(PdfFontProgramLayout layout, ReadOnlyFontStream fontStream)
    {
        if (fontStream.Length == 0)
        {
            return null;
        }

        try
        {
            fontStream.Position = 0;

            switch (layout)
            {
                case PdfFontProgramLayout.Sfnt:
                {
                    return LoadSfnt(fontStream);
                }
                case PdfFontProgramLayout.Type1:
                {
                    return LoadType1(fontStream);
                }
                default:
                {
                    return LoadCff(fontStream);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Embedded font program does not parse as {Layout}.", layout);
            return null;
        }
#pragma warning restore CA1031
    }

    private IPdfTypeface LoadSfnt(ReadOnlyFontStream fontStream)
    {
        SfntContainerProcessor containerProcessor = new(_loggerFactory.CreateLogger<SfntContainerProcessor>());
        SfntContainer? container = containerProcessor.Read(fontStream, ttcIndex: 0);
        SfntTableRecord? cffRecord = container?.FindTable(SfntTableTags.Cff);

        // A composite font addresses glyphs by index, so an sfnt wrapper around CFF outlines only
        // earns its place when it carries the tables that describe the font as a whole.
        if (container != null && cffRecord != null && (!_isComposite || !HasSfntTables(container)))
        {
            SfntCffProcessor cffProcessor = new(_loggerFactory);
            CffTypeface? cffTypeface = cffProcessor.Read(fontStream.GetMemory(cffRecord.Value));

            if (cffTypeface == null)
            {
                throw new InvalidDataException("The sfnt 'CFF ' table is not a valid CFF font program.");
            }

            fontStream.Dispose();

            return new CffPdfTypeface(cffTypeface);
        }

        return new SfntPdfTypeface(fontStream, _loggerFactory);
    }

    private CffPdfTypeface LoadType1(ReadOnlyFontStream fontStream)
    {
        // The eexec-encrypted section is decrypted as one block and converted to CFF up front, so
        // there is no part of a Type1 program this can leave unread.
        ReadOnlyMemory<byte> fontProgram = fontStream.GetMemory(0, (int)fontStream.Length);

        Type1RawFontProgram rawProgram = new(
            fontProgram,
            _fontDescriptor.FontFileLength1,
            _fontDescriptor.FontFileLength2,
            _fontDescriptor.FontFileLength3);

        CffTypeface? cffTypeface = Type1ToCffConverter.GetCffFont(rawProgram, _loggerFactory);

        if (cffTypeface == null)
        {
            throw new InvalidDataException("Data is not a valid Type1 font program.");
        }

        fontStream.Dispose();

        return new CffPdfTypeface(cffTypeface);
    }

    private CffPdfTypeface LoadCff(Stream fontStream) => new(fontStream, _loggerFactory);

    private static bool HasSfntTables(SfntContainer container)
    {
        return container.FindTable(SfntTableTags.Head) != null
            && container.FindTable(SfntTableTags.Hhea) != null
            && container.FindTable(SfntTableTags.Maxp) != null
            && container.FindTable(SfntTableTags.Post) != null;
    }

    private static PdfFontProgramLayout DetectLayout(ReadOnlyFontStream fontStream)
    {
        ReadOnlySpan<byte> fontProgram = fontStream.GetMemory(0, LayoutProbeBytes).Span;

        if (LooksLikeSfnt(fontProgram))
        {
            return PdfFontProgramLayout.Sfnt;
        }

        if (LooksLikeType1(fontProgram))
        {
            return PdfFontProgramLayout.Type1;
        }

        return PdfFontProgramLayout.Cff;
    }

    // TrueType (0x00010000), CFF-flavored OpenType ("OTTO"), older Mac TrueType ("true"), and a
    // TrueType Collection ("ttcf") all start with one of these four-byte sfnt version tags.
    private static bool LooksLikeSfnt(in ReadOnlySpan<byte> fontProgram)
    {
        if (fontProgram.Length < 4)
        {
            return false;
        }

        uint tag = BinaryPrimitives.ReadUInt32BigEndian(fontProgram);

        return tag == 0x00010000
            || tag == 0x4F54544F
            || tag == 0x74727565
            || tag == 0x74746366;
    }

    // A PFB-wrapped program opens with a segment marker (0x80) followed by the ASCII segment type;
    // a PFA-style one opens with the PostScript "%!" document structuring prefix.
    private static bool LooksLikeType1(in ReadOnlySpan<byte> fontProgram)
    {
        if (fontProgram.Length >= 2 && fontProgram[0] == 0x80 && fontProgram[1] == 0x01)
        {
            return true;
        }

        int position = 0;
        while (position < fontProgram.Length && IsLeadingWhitespace(fontProgram[position]))
        {
            position++;
        }

        return fontProgram.Slice(position).StartsWith("%!"u8);
    }

    private static bool IsLeadingWhitespace(byte value) => value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A || value == 0x00;

    /// <summary>
    /// The layouts an embedded font program can physically have, independent of the format its font
    /// descriptor declares.
    /// </summary>
    private enum PdfFontProgramLayout
    {
        /// <summary>
        /// An sfnt font program: TrueType, or CFF-flavored OpenType.
        /// </summary>
        Sfnt,

        /// <summary>
        /// A PostScript Type1 font program, either PFA-style or PFB-wrapped.
        /// </summary>
        Type1,

        /// <summary>
        /// A bare (unwrapped) CFF font program.
        /// </summary>
        Cff
    }
}
