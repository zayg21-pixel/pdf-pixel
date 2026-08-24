using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes a full SFNT font through its per-table models: decodes the container via
/// <see cref="SfntContainerProcessor"/>, then parses every table that has a dedicated processor
/// (head, hhea, maxp, hmtx, OS/2, name, cmap, post, gasp, glyf/loca, CFF). Tables without one remain
/// accessible only as a byte range on <see cref="SfntFont.Tables"/>, and writing drops them.
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
    private readonly SfntGaspProcessor _gaspProcessor;
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
        _gaspProcessor = new SfntGaspProcessor(loggerFactory.CreateLogger<SfntGaspProcessor>());
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
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

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

        SfntTableRecord? cffRecord = container.FindTable(SfntTableTags.Cff);
        SfntTableRecord? locaRecord = container.FindTable(SfntTableTags.Loca);
        SfntTableRecord? glyfRecord = container.FindTable(SfntTableTags.Glyf);

        // Every remaining table is sized by the glyph count, so "maxp" is synthesized rather than
        // left absent - a font without one is otherwise unusable even when its outlines are intact.
        SfntMaxp maxp;
        SfntTableRecord? maxpRecord = container.FindTable(SfntTableTags.Maxp);
        if (maxpRecord != null)
        {
            maxp = _maxpProcessor.Read(stream.GetMemory(maxpRecord.Value));
        }
        else
        {
            _logger.LogWarning("Font has no 'maxp' table; synthesizing one from the glyph count 'loca' implies.");
            maxp = SfntMaxpProcessor.CreateDefault(numGlyphs: 0, hasTrueTypeOutlines: cffRecord == null);
        }

        if (maxp.NumGlyphs == 0)
        {
            maxp.NumGlyphs = DeriveNumGlyphs(locaRecord, font.Head);
        }

        font.Maxp = maxp;

        SfntTableRecord? hmtxRecord = container.FindTable(SfntTableTags.Hmtx);

        // Writing drops "hmtx" when there is no "hhea" to state how many long entries it holds, so a
        // font whose metrics are intact would still lose them all to a missing header.
        SfntHhea hhea;
        SfntTableRecord? hheaRecord = container.FindTable(SfntTableTags.Hhea);
        if (hheaRecord != null)
        {
            hhea = _hheaProcessor.Read(stream.GetMemory(hheaRecord.Value));
        }
        else
        {
            _logger.LogWarning("Font has no 'hhea' table; synthesizing one from the vertical bounds 'head' states.");
            hhea = SfntHheaProcessor.CreateDefault(
                ascender: font.Head?.YMax ?? 0,
                descender: font.Head?.YMin ?? 0,
                numberOfHMetrics: 0);
        }

        ushort longEntryLimit = DeriveLongEntryLimit(hmtxRecord, maxp.NumGlyphs);
        if (hmtxRecord != null && (hhea.NumberOfHMetrics == 0 || hhea.NumberOfHMetrics > longEntryLimit))
        {
            // Recovers a header that states no long entries at all, and caps one that states more
            // than "hmtx" has the bytes to hold, so the count always describes entries that exist.
            hhea.NumberOfHMetrics = longEntryLimit;
        }

        if (hhea.NumberOfHMetrics > maxp.NumGlyphs)
        {
            // "hmtx" holds one long entry per glyph at most, so a numberOfHMetrics past numGlyphs
            // describes entries that cannot exist. Clamping it here - rather than only where "hmtx" is
            // read - keeps the two tables telling the same story when the font is written back out.
            hhea.NumberOfHMetrics = maxp.NumGlyphs;
        }

        font.Hhea = hhea;

        if (hmtxRecord != null)
        {
            font.Hmtx = _hmtxProcessor.Read(stream.GetMemory(hmtxRecord.Value), hhea.NumberOfHMetrics, maxp.NumGlyphs);
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

        SfntTableRecord? gaspRecord = container.FindTable(SfntTableTags.Gasp);
        if (gaspRecord != null)
        {
            font.Gasp = _gaspProcessor.Read(stream.GetMemory(gaspRecord.Value));
        }

        if (cffRecord != null)
        {
            font.CffTypeface = _cffProcessor.Read(stream.GetMemory(cffRecord.Value));
        }

        if (locaRecord != null && glyfRecord != null && font.Head != null && font.Maxp != null)
        {
            SfntLoca loca = _locaProcessor.Read(stream.GetMemory(locaRecord.Value), font.Maxp.NumGlyphs, font.Head.IndexToLocFormat, glyfRecord.Value.Length);
            font.Glyf = new SfntGlyf { Loca = loca };
            font.GlyfRecord = glyfRecord;
        }

        return font;
    }

    /// <summary>
    /// Recovers the glyph count from "loca"'s own length, which holds one entry per glyph plus a
    /// trailing sentinel, at the entry width "head" states. Returns 0 when the font has neither -
    /// leaving the count unknown rather than guessing one the outlines cannot back.
    /// </summary>
    private static ushort DeriveNumGlyphs(SfntTableRecord? locaRecord, SfntHead? head)
    {
        if (locaRecord == null || head == null)
        {
            return 0;
        }

        int entryLength = (head.IndexToLocFormat != 0) ? 4 : 2;
        int entryCount = locaRecord.Value.Length / entryLength;
        if (entryCount < 2)
        {
            return 0;
        }

        return (ushort)Math.Min(entryCount - 1, ushort.MaxValue);
    }

    /// <summary>
    /// Derives the most long entries "hmtx" could hold, from its own length. Every glyph past the
    /// last long entry carries a side bearing alone, so a conforming table is always at least four
    /// bytes per long entry - making its length divided by four a ceiling that never cuts into a
    /// count the table can actually back. Returns 0 when the font has no "hmtx" to size it from.
    /// </summary>
    private static ushort DeriveLongEntryLimit(SfntTableRecord? hmtxRecord, ushort numGlyphs)
    {
        if (hmtxRecord == null)
        {
            return 0;
        }

        return (ushort)Math.Min(hmtxRecord.Value.Length / 4, numGlyphs);
    }

    /// <summary>
    /// Resolves a single glyph's path on demand, caching the result on <see cref="SfntFont.Glyf"/>.
    /// Returns an empty path if <paramref name="font"/> has no "glyf" table.
    /// </summary>
    /// <param name="font">The font to resolve the glyph from.</param>
    /// <param name="gid">The glyph ID to resolve.</param>
    /// <param name="stream">The stream <paramref name="font"/> was read from.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public ReadOnlyMemory<byte> ResolvePath(SfntFont font, int gid, ReadOnlyFontStream stream, in PdfFontMatrix matrix)
    {
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        if (font.Glyf == null || font.GlyfRecord == null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return _glyfProcessor.ResolvePath(font.Glyf, gid, new SfntGlyfSource(stream, font.GlyfRecord.Value), matrix);
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
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        if (font.CmapRecord == null)
        {
            return null;
        }

        return _cmapProcessor.GetGid(subtable, code, new SfntCmapSource(stream, font.CmapRecord.Value));
    }

    /// <summary>
    /// Writes a full SFNT font back to bytes. Tables with a dedicated model are re-serialized from it
    /// and every other source table is dropped. "post", and "cmap" when
    /// <paramref name="repackParameters"/> states no mapping, always get a minimal empty stub. "OS/2"
    /// falls back to a stub of its own when the source carries none. "gasp" gets no stub in turn: a
    /// font that carries none is written without it.
    /// </summary>
    /// <param name="font">The font to write.</param>
    /// <param name="sourceStream">The stream <paramref name="font"/> was read from, read for the source glyph bytes.</param>
    /// <param name="repackParameters">The glyph subset and character mapping to write. Null writes every glyph at the id it already had, and no character mapping.</param>
    public byte[] Write(SfntFont font, ReadOnlyFontStream sourceStream, SfntPdfTypefaceRepackParameters? repackParameters = null)
    {
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        if (sourceStream == null)
        {
            throw new ArgumentNullException(nameof(sourceStream));
        }

        IReadOnlyList<ushort>? glyphOrder = repackParameters?.GlyphOrder;

        List<SfntTableData> tables = [];

        if (font.Head != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Head, _headProcessor.Write(font.Head)));
        }

        ushort numGlyphs = (glyphOrder != null) ? (ushort)glyphOrder.Count : (font.Maxp?.NumGlyphs ?? 0);

        if (font.Hhea != null)
        {
            ushort numberOfHMetrics = (glyphOrder != null) ? numGlyphs : font.Hhea.NumberOfHMetrics;
            tables.Add(new SfntTableData(SfntTableTags.Hhea, _hheaProcessor.Write(font.Hhea, numberOfHMetrics)));
        }

        if (font.Maxp != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Maxp, _maxpProcessor.Write(font.Maxp, numGlyphs)));
        }

        if (font.Hmtx != null && font.Hhea != null)
        {
            SfntHmtx hmtx = (glyphOrder != null) ? ReorderHmtx(font.Hmtx, glyphOrder) : font.Hmtx;
            ushort numberOfHMetrics = (glyphOrder != null) ? numGlyphs : font.Hhea.NumberOfHMetrics;
            tables.Add(new SfntTableData(SfntTableTags.Hmtx, _hmtxProcessor.Write(hmtx, numberOfHMetrics)));
        }

        if (font.Os2 != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Os2, _os2Processor.Write(font.Os2)));
        }
        else
        {
            tables.Add(new SfntTableData(SfntTableTags.Os2, SfntOs2Processor.CreateEmptyStub()));
        }

        if (font.Name != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Name, _nameProcessor.Write(font.Name)));
        }
        else
        {
            tables.Add(new SfntTableData(SfntTableTags.Name, SfntNameProcessor.CreateEmptyStub()));
        }

        if (font.CffTypeface != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Cff, _cffProcessor.Write(font.CffTypeface)));
        }

        if (font.Glyf != null && font.Head != null)
        {
            if (font.GlyfRecord != null)
            {
                SfntGlyfWriteResult glyfResult = _glyfProcessor.Write(font.Glyf, new SfntGlyfSource(sourceStream, font.GlyfRecord.Value), glyphOrder);
                tables.Add(new SfntTableData(SfntTableTags.Glyf, glyfResult.GlyfData));

                byte[] locaData = _locaProcessor.Write(glyfResult.Loca, font.Head.IndexToLocFormat);
                tables.Add(new SfntTableData(SfntTableTags.Loca, locaData));
            }
        }

        if (font.Gasp != null)
        {
            tables.Add(new SfntTableData(SfntTableTags.Gasp, _gaspProcessor.Write(font.Gasp)));
        }

        tables.Add(new SfntTableData(SfntTableTags.Cmap, WriteCmap(repackParameters)));
        tables.Add(new SfntTableData(SfntTableTags.Post, SfntPostProcessor.CreateEmptyStub()));

        return _containerProcessor.Write(font.Version, tables);
    }

    /// <summary>
    /// Writes the "cmap" <paramref name="repackParameters"/> states, or an empty placeholder when it
    /// states none.
    /// </summary>
    private static byte[] WriteCmap(SfntPdfTypefaceRepackParameters? repackParameters)
    {
        IReadOnlyDictionary<int, ushort>? codeToGid = repackParameters?.CodeToGid;

        if (repackParameters == null || codeToGid == null)
        {
            return SfntCmapProcessor.CreateEmptyStub();
        }

        return SfntCmapGenerator.Write(repackParameters.CmapPlatformId, repackParameters.CmapEncodingId, codeToGid);
    }

    /// <summary>
    /// Builds one metric per entry of <paramref name="glyphOrder"/>, taken from the source metric of
    /// the glyph it names. A named glyph the source has no metric for takes a zero-width one.
    /// </summary>
    private static SfntHmtx ReorderHmtx(SfntHmtx hmtx, IReadOnlyList<ushort> glyphOrder)
    {
        IReadOnlyList<SfntHorizontalMetric> sourceMetrics = hmtx.Metrics;
        var metrics = new SfntHorizontalMetric[glyphOrder.Count];

        for (int gid = 0; gid < glyphOrder.Count; gid++)
        {
            ushort sourceGid = glyphOrder[gid];

            metrics[gid] = (sourceGid < sourceMetrics.Count)
                ? sourceMetrics[sourceGid]
                : new SfntHorizontalMetric(0, 0);
        }

        return new SfntHmtx { Metrics = metrics };
    }
}
