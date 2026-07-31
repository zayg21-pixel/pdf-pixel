using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Streams;
using System;
using System.IO;

namespace PdfPixel.Fonts;

/// <summary>
/// Loads an <see cref="IPdfTypeface"/> from an embedded PDF font program, either a bare CFF program
/// (Type1C, CIDFontType0C) or an sfnt font program (TrueType, or CFF-flavored OpenType).
/// </summary>
public sealed class PdfTypefaceLoader
{
    private readonly bool _isComposite;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTypefaceLoader"/> class for fonts of the given
    /// PDF font subtype.
    /// </summary>
    /// <param name="fontSubType">The PDF font dictionary's /Subtype.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public PdfTypefaceLoader(PdfFontSubType fontSubType, ILoggerFactory loggerFactory)
    {
        _isComposite = fontSubType is PdfFontSubType.CidFontType0 or PdfFontSubType.CidFontType2;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Parses a bare (unwrapped) CFF font program - a Type1C or CIDFontType0C embedded font.
    /// </summary>
    /// <param name="cffBytes">The raw CFF font program bytes.</param>
    public IPdfTypeface GetTypefaceFromCff(in ReadOnlyMemory<byte> cffBytes)
    {
        CffTypefaceReader reader = new(_loggerFactory);
        CffTypeface? cffTypeface = reader.Read(cffBytes);

        if (cffTypeface == null)
        {
            throw new ArgumentException("Data is not a valid CFF font program.", nameof(cffBytes));
        }

        return new CffPdfTypeface(cffTypeface);
    }

    /// <summary>
    /// Decodes <paramref name="fontFileStream"/>'s sfnt font program (TrueType, or CFF-flavored
    /// OpenType) and returns the typeface it should be loaded as, unwrapping to a bare CFF typeface
    /// when the sfnt wrapper isn't needed.
    /// </summary>
    public IPdfTypeface GetTypefaceFromSfnt(PdfObjectStream? fontFileStream)
    {
        Stream sfntStream = fontFileStream?.DecodeAsStream() ?? Stream.Null;
        ReadOnlyFontStream stream = ReadOnlyFontStream.Create(sfntStream, leaveOpen: false);

        SfntContainerProcessor containerProcessor = new(_loggerFactory.CreateLogger<SfntContainerProcessor>());
        SfntContainer? container = containerProcessor.Read(stream, ttcIndex: 0);
        SfntTableRecord? cffRecord = container?.FindTable(SfntTableTags.Cff);

        if (container != null && cffRecord != null && (!_isComposite || !HasSfntTables(container)))
        {
            ReadOnlyMemory<byte> cffProgram = stream.GetMemory(cffRecord.Value);
            stream.Dispose();

            return GetTypefaceFromCff(cffProgram);
        }

        return new SfntPdfTypeface(stream, _loggerFactory);
    }

    private static bool HasSfntTables(SfntContainer container)
    {
        return container.FindTable(SfntTableTags.Head) != null
            && container.FindTable(SfntTableTags.Hhea) != null
            && container.FindTable(SfntTableTags.Maxp) != null
            && container.FindTable(SfntTableTags.Post) != null;
    }
}
