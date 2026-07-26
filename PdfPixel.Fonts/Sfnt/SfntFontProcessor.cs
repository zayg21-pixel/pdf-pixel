using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes a full SFNT font through its per-table models: decodes the container via
/// <see cref="SfntContainerProcessor"/>, then parses every table that has a dedicated processor
/// (head, hhea, maxp, hmtx, OS/2, name, cmap, post, glyf/loca, CFF). Tables without one remain
/// accessible only as raw bytes on <see cref="SfntFont.Tables"/>, and are passed through unchanged
/// when writing - as are "cmap" and "post", which are read-only and have no writer of their own.
/// </summary>
public class SfntFontProcessor
{
    private readonly ILogger<SfntFontProcessor> _logger;
    private readonly SfntContainerProcessor _containerProcessor;
    private readonly SfntHeadProcessor _headProcessor;
    private readonly SfntHheaProcessor _hheaProcessor;
    private readonly SfntMaxpProcessor _maxpProcessor;
    private readonly SfntHmtxProcessor _hmtxProcessor;
    private readonly SfntOs2Processor _os2Processor;
    private readonly SfntNameProcessor _nameProcessor;
    private readonly SfntCmapProcessor _cmapProcessor;
    private readonly SfntPostProcessor _postProcessor;
    private readonly SfntLocaProcessor _locaProcessor;
    private readonly SfntGlyfProcessor _glyfProcessor;
    private readonly SfntCffProcessor _cffProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntFontProcessor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SfntFontProcessor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SfntFontProcessor>();
        _containerProcessor = new SfntContainerProcessor(loggerFactory.CreateLogger<SfntContainerProcessor>());
        _headProcessor = new SfntHeadProcessor(loggerFactory.CreateLogger<SfntHeadProcessor>());
        _hheaProcessor = new SfntHheaProcessor(loggerFactory.CreateLogger<SfntHheaProcessor>());
        _maxpProcessor = new SfntMaxpProcessor(loggerFactory.CreateLogger<SfntMaxpProcessor>());
        _hmtxProcessor = new SfntHmtxProcessor(loggerFactory.CreateLogger<SfntHmtxProcessor>());
        _os2Processor = new SfntOs2Processor(loggerFactory.CreateLogger<SfntOs2Processor>());
        _nameProcessor = new SfntNameProcessor(loggerFactory.CreateLogger<SfntNameProcessor>());
        _cmapProcessor = new SfntCmapProcessor(loggerFactory.CreateLogger<SfntCmapProcessor>());
        _postProcessor = new SfntPostProcessor(loggerFactory.CreateLogger<SfntPostProcessor>());
        _locaProcessor = new SfntLocaProcessor(loggerFactory.CreateLogger<SfntLocaProcessor>());
        _glyfProcessor = new SfntGlyfProcessor(loggerFactory);
        _cffProcessor = new SfntCffProcessor(loggerFactory);
    }

    /// <summary>
    /// Reads a full SFNT font from <paramref name="stream"/>. Returns null if the container itself is
    /// unparsable; a table with a dedicated processor that fails to parse is left null on the
    /// result rather than failing the whole read.
    /// </summary>
    public SfntFont? Read(ReadOnlyFontStream stream) => Read(stream, ttcIndex: 0);

    /// <summary>
    /// Reads a full SFNT font from <paramref name="stream"/>, at the given font index within a
    /// TrueType Collection ("ttcf") file. Returns null if the container itself is unparsable; a table
    /// with a dedicated processor that fails to parse is left null on the result rather than failing
    /// the whole read.
    /// </summary>
    public SfntFont? Read(ReadOnlyFontStream stream, int ttcIndex)
    {
        SfntContainer? container = _containerProcessor.Read(stream, ttcIndex);
        if (container == null)
        {
            _logger.LogWarning("Failed to read sfnt font: container is unparsable.");
            return null;
        }

        SfntFont font = new()
        {
            Version = container.Version,
            Tables = new List<SfntTableRecord>(container.Tables)
        };

        SfntTableRecord? headRecord = container.FindTable(SfntTableTags.Head);
        if (headRecord != null)
        {
            font.Head = _headProcessor.Read(stream.GetMemory(headRecord.Value));
        }

        SfntTableRecord? hheaRecord = container.FindTable(SfntTableTags.Hhea);
        if (hheaRecord != null)
        {
            font.Hhea = _hheaProcessor.Read(stream.GetMemory(hheaRecord.Value));
        }

        SfntTableRecord? maxpRecord = container.FindTable(SfntTableTags.Maxp);
        if (maxpRecord != null)
        {
            font.Maxp = _maxpProcessor.Read(stream.GetMemory(maxpRecord.Value));
        }

        SfntTableRecord? hmtxRecord = container.FindTable(SfntTableTags.Hmtx);
        if (hmtxRecord != null && font.Hhea != null && font.Maxp != null)
        {
            font.Hmtx = _hmtxProcessor.Read(stream.GetMemory(hmtxRecord.Value), font.Hhea.NumberOfHMetrics, font.Maxp.NumGlyphs);
        }

        SfntTableRecord? os2Record = container.FindTable(SfntTableTags.Os2);
        if (os2Record != null)
        {
            font.Os2 = _os2Processor.Read(stream.GetMemory(os2Record.Value));
        }

        SfntTableRecord? nameRecord = container.FindTable(SfntTableTags.Name);
        if (nameRecord != null)
        {
            font.Name = _nameProcessor.Read(stream.GetMemory(nameRecord.Value));
        }

        SfntTableRecord? cmapRecord = container.FindTable(SfntTableTags.Cmap);
        if (cmapRecord != null)
        {
            font.Cmap = _cmapProcessor.Read(new SfntCmapSource(stream, cmapRecord.Value));
            font.CmapRecord = cmapRecord;
        }

        SfntTableRecord? postRecord = container.FindTable(SfntTableTags.Post);
        if (postRecord != null)
        {
            font.Post = _postProcessor.Read(stream.GetMemory(postRecord.Value));
        }

        SfntTableRecord? cffRecord = container.FindTable(SfntTableTags.Cff);
        if (cffRecord != null)
        {
            font.CffTypeface = _cffProcessor.Read(stream.GetMemory(cffRecord.Value));
        }

        SfntTableRecord? locaRecord = container.FindTable(SfntTableTags.Loca);
        SfntTableRecord? glyfRecord = container.FindTable(SfntTableTags.Glyf);
        if (locaRecord != null && glyfRecord != null && font.Head != null && font.Maxp != null)
        {
            SfntLoca? loca = _locaProcessor.Read(stream.GetMemory(locaRecord.Value), font.Maxp.NumGlyphs, font.Head.IndexToLocFormat);
            if (loca != null)
            {
                font.Glyf = new SfntGlyf { Loca = loca };
                font.GlyfRecord = glyfRecord;
            }
        }

        return font;
    }

    /// <summary>
    /// Resolves a single glyph's outline on demand, caching the result on <see cref="SfntFont.Glyf"/>.
    /// Returns null if <paramref name="font"/> has no "glyf" table.
    /// </summary>
    /// <param name="font">The font to resolve the glyph from.</param>
    /// <param name="gid">The glyph ID to resolve.</param>
    /// <param name="stream">The stream <paramref name="font"/> was read from.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public SfntGlyphCharacter? ResolveGlyph(SfntFont font, int gid, ReadOnlyFontStream stream, in PdfFontMatrix matrix)
    {
        if (font.Glyf == null || font.GlyfRecord == null)
        {
            return null;
        }

        return _glyfProcessor.ResolveGlyph(font.Glyf, gid, new SfntGlyfSource(stream, font.GlyfRecord.Value), matrix);
    }

    /// <summary>
    /// Resolves a character code to a glyph id via <paramref name="subtable"/> (one of <see cref="SfntFont.Cmap"/>'s
    /// subtables), parsing and caching its ranges on first query. Returns null if <paramref name="font"/>
    /// has no "cmap" table.
    /// </summary>
    /// <param name="font">The font <paramref name="subtable"/> belongs to.</param>
    /// <param name="subtable">The subtable to query.</param>
    /// <param name="code">The character code to resolve.</param>
    /// <param name="stream">The stream <paramref name="font"/> was read from.</param>
    public ushort? GetCmapGid(SfntFont font, SfntCmapSubtable subtable, int code, ReadOnlyFontStream stream)
    {
        if (font.CmapRecord == null)
        {
            return null;
        }

        return _cmapProcessor.GetGid(subtable, code, new SfntCmapSource(stream, font.CmapRecord.Value));
    }

    /// <summary>
    /// Writes a full SFNT font back to bytes. Every table with a dedicated model
    /// (<see cref="SfntFont.Head"/>, <see cref="SfntFont.Hhea"/>, <see cref="SfntFont.Maxp"/>,
    /// <see cref="SfntFont.Hmtx"/>, <see cref="SfntFont.Os2"/>, <see cref="SfntFont.Name"/>,
    /// <see cref="SfntFont.CffTypeface"/>, <see cref="SfntFont.Glyf"/>) is re-serialized from that model whenever it is set - regardless
    /// of whether <see cref="SfntFont.Tables"/> already has a raw entry for it, so setting a model is
    /// enough to produce its table even when assembling a brand new font. Every other entry in
    /// <see cref="SfntFont.Tables"/> (e.g. a repacked "CFF " table, or "cmap"/"post" when a model
    /// couldn't be or wasn't parsed) is passed through unchanged, resolved from <paramref name="sourceStream"/>
    /// (the stream <paramref name="font"/> was read from). "cmap" and "post" are themselves read-only
    /// and have no writer of their own - if neither a model nor a raw entry produces one, a minimal
    /// empty stub is added instead, since both are required for a valid OTTO container.
    /// </summary>
    /// <param name="font">The font to write.</param>
    /// <param name="sourceStream">The stream <paramref name="font"/> was read from, used to resolve passthrough tables' bytes.</param>
    public byte[] Write(SfntFont font, ReadOnlyFontStream sourceStream)
    {
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        List<SfntTableData> tables = [];
        HashSet<uint> writtenTags = [];

        if (font.Head != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Head, _headProcessor.Write(font.Head)));
            writtenTags.Add(SfntTableTags.Head.Value);
        }

        if (font.Hhea != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Hhea, _hheaProcessor.Write(font.Hhea)));
            writtenTags.Add(SfntTableTags.Hhea.Value);
        }

        if (font.Maxp != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Maxp, _maxpProcessor.Write(font.Maxp)));
            writtenTags.Add(SfntTableTags.Maxp.Value);
        }

        if (font.Hmtx != null && font.Hhea != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Hmtx, _hmtxProcessor.Write(font.Hmtx, font.Hhea.NumberOfHMetrics)));
            writtenTags.Add(SfntTableTags.Hmtx.Value);
        }

        if (font.Os2 != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Os2, _os2Processor.Write(font.Os2)));
            writtenTags.Add(SfntTableTags.Os2.Value);
        }

        if (font.Name != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Name, _nameProcessor.Write(font.Name)));
            writtenTags.Add(SfntTableTags.Name.Value);
        }

        if (font.CffTypeface != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Cff, _cffProcessor.Write(font.CffTypeface)));
            writtenTags.Add(SfntTableTags.Cff.Value);
        }

        if (font.Glyf != null && font.Head != null)
        {
            if (font.GlyfRecord != null)
            {
                SfntGlyfWriteResult glyfResult = _glyfProcessor.Write(font.Glyf, new SfntGlyfSource(sourceStream, font.GlyfRecord.Value), PdfFontMatrix.Identity);
                tables.Add(new SfntTableData(SfntTableTags.Glyf, glyfResult.GlyfData));
                writtenTags.Add(SfntTableTags.Glyf.Value);

                byte[] locaData = _locaProcessor.Write(glyfResult.Loca, font.Head.IndexToLocFormat);
                tables.Add(new SfntTableData(SfntTableTags.Loca, locaData));
                writtenTags.Add(SfntTableTags.Loca.Value);
            }
        }

        foreach (SfntTableRecord table in font.Tables)
        {
            if (writtenTags.Add(table.Tag.Value))
            {
                tables.Add(new SfntTableData(table.Tag, sourceStream.GetMemory(table)));
            }
        }

        if (writtenTags.Add(SfntTableTags.Cmap.Value))
        {
            tables.Add(new SfntTableData(SfntTableTags.Cmap, SfntCmapProcessor.CreateEmptyStub()));
        }

        if (writtenTags.Add(SfntTableTags.Post.Value))
        {
            tables.Add(new SfntTableData(SfntTableTags.Post, SfntPostProcessor.CreateEmptyStub()));
        }

        return _containerProcessor.Write(font.Version, tables);
    }
}
