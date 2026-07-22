using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes a full SFNT font through its per-table models: decodes the container via
/// <see cref="SfntContainerProcessor"/>, then parses every table that has a dedicated processor
/// (head, hhea, maxp, hmtx, OS/2, name, cmap, post, glyf/loca). Tables without one remain accessible
/// only as raw bytes on <see cref="SfntFont.Tables"/>, and are passed through unchanged when writing -
/// as are "cmap" and "post", which are read-only and have no writer of their own.
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
    }

    /// <summary>
    /// Reads a full SFNT font from its raw bytes. Returns null if the container itself is
    /// unparsable; a table with a dedicated processor that fails to parse is left null on the
    /// result rather than failing the whole read.
    /// </summary>
    public SfntFont? Read(in ReadOnlyMemory<byte> data)
    {
        SfntContainer? container = _containerProcessor.Read(data);
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
            font.Head = _headProcessor.Read(headRecord.Value.Data);
        }

        SfntTableRecord? hheaRecord = container.FindTable(SfntTableTags.Hhea);
        if (hheaRecord != null)
        {
            font.Hhea = _hheaProcessor.Read(hheaRecord.Value.Data);
        }

        SfntTableRecord? maxpRecord = container.FindTable(SfntTableTags.Maxp);
        if (maxpRecord != null)
        {
            font.Maxp = _maxpProcessor.Read(maxpRecord.Value.Data);
        }

        SfntTableRecord? hmtxRecord = container.FindTable(SfntTableTags.Hmtx);
        if (hmtxRecord != null && font.Hhea != null && font.Maxp != null)
        {
            font.Hmtx = _hmtxProcessor.Read(hmtxRecord.Value.Data, font.Hhea.NumberOfHMetrics, font.Maxp.NumGlyphs);
        }

        SfntTableRecord? os2Record = container.FindTable(SfntTableTags.Os2);
        if (os2Record != null)
        {
            font.Os2 = _os2Processor.Read(os2Record.Value.Data);
        }

        SfntTableRecord? nameRecord = container.FindTable(SfntTableTags.Name);
        if (nameRecord != null)
        {
            font.Name = _nameProcessor.Read(nameRecord.Value.Data);
        }

        SfntTableRecord? cmapRecord = container.FindTable(SfntTableTags.Cmap);
        if (cmapRecord != null)
        {
            font.Cmap = _cmapProcessor.Read(cmapRecord.Value.Data);
        }

        SfntTableRecord? postRecord = container.FindTable(SfntTableTags.Post);
        if (postRecord != null)
        {
            font.Post = _postProcessor.Read(postRecord.Value.Data);
        }

        SfntTableRecord? locaRecord = container.FindTable(SfntTableTags.Loca);
        SfntTableRecord? glyfRecord = container.FindTable(SfntTableTags.Glyf);
        if (locaRecord != null && glyfRecord != null && font.Head != null && font.Maxp != null)
        {
            SfntLoca? loca = _locaProcessor.Read(locaRecord.Value.Data, font.Maxp.NumGlyphs, font.Head.IndexToLocFormat);
            if (loca != null)
            {
                font.Glyf = _glyfProcessor.Read(glyfRecord.Value.Data, loca, PdfFontMatrix.Identity);
            }
        }

        return font;
    }

    /// <summary>
    /// Writes a full SFNT font back to bytes. Every table with a dedicated model
    /// (<see cref="SfntFont.Head"/>, <see cref="SfntFont.Hhea"/>, <see cref="SfntFont.Maxp"/>,
    /// <see cref="SfntFont.Hmtx"/>, <see cref="SfntFont.Os2"/>, <see cref="SfntFont.Name"/>,
    /// <see cref="SfntFont.Glyf"/>) is re-serialized from that model whenever it is set - regardless
    /// of whether <see cref="SfntFont.Tables"/> already has a raw entry for it, so setting a model is
    /// enough to produce its table even when assembling a brand new font. Every other entry in
    /// <see cref="SfntFont.Tables"/> (e.g. a repacked "CFF " table, or "cmap"/"post" when a model
    /// couldn't be or wasn't parsed) is passed through unchanged. "cmap" and "post" are themselves
    /// read-only and have no writer of their own - if neither a model nor a raw entry produces one,
    /// a minimal empty stub is added instead, since both are required for a valid OTTO container.
    /// </summary>
    public byte[] Write(SfntFont font)
    {
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        List<SfntTableRecord> tables = [];
        HashSet<uint> writtenTags = [];

        if (font.Head != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Head, 0, _headProcessor.Write(font.Head)));
            writtenTags.Add(SfntTableTags.Head.Value);
        }

        if (font.Hhea != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Hhea, 0, _hheaProcessor.Write(font.Hhea)));
            writtenTags.Add(SfntTableTags.Hhea.Value);
        }

        if (font.Maxp != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Maxp, 0, _maxpProcessor.Write(font.Maxp)));
            writtenTags.Add(SfntTableTags.Maxp.Value);
        }

        if (font.Hmtx != null && font.Hhea != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Hmtx, 0, _hmtxProcessor.Write(font.Hmtx, font.Hhea.NumberOfHMetrics)));
            writtenTags.Add(SfntTableTags.Hmtx.Value);
        }

        if (font.Os2 != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Os2, 0, _os2Processor.Write(font.Os2)));
            writtenTags.Add(SfntTableTags.Os2.Value);
        }

        if (font.Name != null)
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Name, 0, _nameProcessor.Write(font.Name)));
            writtenTags.Add(SfntTableTags.Name.Value);
        }

        if (font.Glyf != null && font.Head != null)
        {
            SfntGlyfWriteResult glyfResult = _glyfProcessor.Write(font.Glyf);
            tables.Add(new SfntTableRecord(SfntTableTags.Glyf, 0, glyfResult.GlyfData));
            writtenTags.Add(SfntTableTags.Glyf.Value);

            byte[] locaData = _locaProcessor.Write(glyfResult.Loca, font.Head.IndexToLocFormat);
            tables.Add(new SfntTableRecord(SfntTableTags.Loca, 0, locaData));
            writtenTags.Add(SfntTableTags.Loca.Value);
        }

        foreach (SfntTableRecord table in font.Tables)
        {
            if (writtenTags.Add(table.Tag.Value))
            {
                tables.Add(table);
            }
        }

        if (writtenTags.Add(SfntTableTags.Cmap.Value))
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Cmap, 0, SfntCmapProcessor.CreateEmptyStub()));
        }

        if (writtenTags.Add(SfntTableTags.Post.Value))
        {
            tables.Add(new SfntTableRecord(SfntTableTags.Post, 0, SfntPostProcessor.CreateEmptyStub()));
        }

        return _containerProcessor.Write(font.Version, tables);
    }
}
