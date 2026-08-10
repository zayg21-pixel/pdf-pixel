using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Type1;
using PdfPixel.Fonts.Typeface;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

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
    private static readonly PdfFontProgramLayout[] AllLayouts =
    [
        PdfFontProgramLayout.Sfnt,
        PdfFontProgramLayout.Type1,
        PdfFontProgramLayout.Cff
    ];

    private readonly bool _isComposite;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PdfTypefaceLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTypefaceLoader"/> class for fonts of the given
    /// PDF font subtype.
    /// </summary>
    /// <param name="fontSubType">The PDF font dictionary's /Subtype.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public PdfTypefaceLoader(PdfFontSubType fontSubType, ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _isComposite = fontSubType is PdfFontSubType.CidFontType0 or PdfFontSubType.CidFontType2;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfTypefaceLoader>();
    }

    /// <summary>
    /// Parses the font program embedded in <paramref name="fontDescriptor"/>. PDF producers mislabel
    /// embedded programs often enough - an sfnt file declared /Type1C, a PostScript Type1 program
    /// declared /FontFile on a CID-keyed font - that the declared format is used for nothing here:
    /// the layout is detected from the leading bytes, and if that parse fails the remaining layouts
    /// are tried before giving up.
    /// </summary>
    /// <param name="fontDescriptor">The font descriptor holding the embedded font program.</param>
    /// <returns>The typeface the font program parses as.</returns>
    /// <exception cref="ArgumentException">The font program does not parse as any known layout.</exception>
    public IPdfTypeface GetTypefaceFromFontProgram(PdfFontDescriptor fontDescriptor)
    {
        if (fontDescriptor == null)
        {
            throw new ArgumentNullException(nameof(fontDescriptor));
        }

        ReadOnlyMemory<byte> fontProgram = fontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
        PdfFontProgramLayout detectedLayout = DetectLayout(fontProgram.Span);
        IPdfTypeface? typeface = TryLoad(detectedLayout, fontProgram, fontDescriptor);

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

            typeface = TryLoad(fallbackLayout, fontProgram, fontDescriptor);

            if (typeface != null)
            {
                _logger.LogWarning(
                    "Embedded font program declared /{DeclaredFormat} and detected as {DetectedLayout} parsed as {ActualLayout}.",
                    fontDescriptor.FontFileFormat,
                    detectedLayout,
                    fallbackLayout);

                return typeface;
            }
        }

        throw new ArgumentException("Data is not a valid font program.", nameof(fontDescriptor));
    }

    private IPdfTypeface? TryLoad(PdfFontProgramLayout layout, in ReadOnlyMemory<byte> fontProgram, PdfFontDescriptor fontDescriptor)
    {
        if (fontProgram.IsEmpty)
        {
            return null;
        }

        try
        {
            switch (layout)
            {
                case PdfFontProgramLayout.Sfnt:
                {
                    return LoadSfnt(fontProgram);
                }
                case PdfFontProgramLayout.Type1:
                {
                    return LoadType1(fontProgram, fontDescriptor);
                }
                default:
                {
                    return LoadCff(fontProgram);
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

    private IPdfTypeface LoadSfnt(in ReadOnlyMemory<byte> fontProgram)
    {
        ReadOnlyFontStream stream = ReadOnlyFontStream.Create(CreateReadOnlyStream(fontProgram), leaveOpen: false);
        SfntContainerProcessor containerProcessor = new(_loggerFactory.CreateLogger<SfntContainerProcessor>());
        SfntContainer? container = containerProcessor.Read(stream, ttcIndex: 0);
        SfntTableRecord? cffRecord = container?.FindTable(SfntTableTags.Cff);

        // A composite font addresses glyphs by index, so an sfnt wrapper around CFF outlines only
        // earns its place when it carries the tables that describe the font as a whole.
        if (container != null && cffRecord != null && (!_isComposite || !HasSfntTables(container)))
        {
            ReadOnlyMemory<byte> cffProgram = stream.GetMemory(cffRecord.Value);
            stream.Dispose();

            return LoadCff(cffProgram);
        }

        return new SfntPdfTypeface(stream, _loggerFactory);
    }

    private CffPdfTypeface LoadType1(in ReadOnlyMemory<byte> fontProgram, PdfFontDescriptor fontDescriptor)
    {
        Type1RawFontProgram rawProgram = new(
            fontProgram,
            fontDescriptor.FontFileLength1,
            fontDescriptor.FontFileLength2,
            fontDescriptor.FontFileLength3);

        CffTypeface? cffTypeface = Type1ToCffConverter.GetCffFont(rawProgram, _loggerFactory);

        if (cffTypeface == null)
        {
            throw new InvalidDataException("Data is not a valid Type1 font program.");
        }

        return new CffPdfTypeface(cffTypeface);
    }

    private CffPdfTypeface LoadCff(in ReadOnlyMemory<byte> fontProgram)
    {
        CffTypefaceReader reader = new(_loggerFactory);
        CffTypeface? cffTypeface = reader.Read(fontProgram);

        if (cffTypeface == null)
        {
            throw new InvalidDataException("Data is not a valid CFF font program.");
        }

        return new CffPdfTypeface(cffTypeface);
    }

    private static bool HasSfntTables(SfntContainer container)
    {
        return container.FindTable(SfntTableTags.Head) != null
            && container.FindTable(SfntTableTags.Hhea) != null
            && container.FindTable(SfntTableTags.Maxp) != null
            && container.FindTable(SfntTableTags.Post) != null;
    }

    private static MemoryStream CreateReadOnlyStream(in ReadOnlyMemory<byte> fontProgram)
    {
        if (MemoryMarshal.TryGetArray(fontProgram, out ArraySegment<byte> segment) && segment.Array != null)
        {
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false);
        }

        return new MemoryStream(fontProgram.ToArray(), writable: false);
    }

    private static PdfFontProgramLayout DetectLayout(in ReadOnlySpan<byte> fontProgram)
    {
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
