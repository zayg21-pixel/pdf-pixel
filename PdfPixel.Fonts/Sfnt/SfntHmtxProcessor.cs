using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "hmtx" table. Unlike the other table processors,
/// <see cref="Read"/> needs <c>numberOfHMetrics</c> (from "hhea") and the glyph count (from "maxp")
/// as inputs, since "hmtx" is not self-describing: its own compression scheme (trailing glyphs share
/// the last long entry's advance width) can only be undone with those two numbers.
/// </summary>
public class SfntHmtxProcessor
{
    private readonly ILogger<SfntHmtxProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntHmtxProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntHmtxProcessor(ILogger<SfntHmtxProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses an "hmtx" table from its raw content bytes, expanding it to one entry per glyph. Never
    /// fails: once the data runs out an advance width falls back to 0 and a trailing side bearing to
    /// the last long entry's advance width, since the malformed subsetted fonts this table shows up
    /// in still carry their real per-glyph advance widths in the long entries that are present.
    /// </summary>
    /// <param name="data">The "hmtx" table's raw content bytes.</param>
    /// <param name="numberOfHMetrics">The "hhea" table's numberOfHMetrics field. A value past
    /// <paramref name="numGlyphs"/> is clamped to it, since no glyph beyond the last one can carry a
    /// long entry.</param>
    /// <param name="numGlyphs">The "maxp" table's numGlyphs field.</param>
    public SfntHmtx Read(in ReadOnlyMemory<byte> data, ushort numberOfHMetrics, ushort numGlyphs)
    {
        if (numberOfHMetrics > numGlyphs)
        {
            _logger.LogWarning(
                "Reading 'hmtx' table with numberOfHMetrics {NumberOfHMetrics} past numGlyphs {NumGlyphs}; clamped to numGlyphs.",
                numberOfHMetrics,
                numGlyphs);
            numberOfHMetrics = numGlyphs;
        }

        SfntReader reader = new(data.Span);
        var metrics = new SfntHorizontalMetric[numGlyphs];

        ushort lastAdvanceWidth = 0;
        for (int glyphIndex = 0; glyphIndex < numberOfHMetrics; glyphIndex++)
        {
            lastAdvanceWidth = reader.ReadUInt16() ?? 0;
            short leftSideBearing = reader.ReadInt16() ?? 0;
            metrics[glyphIndex] = new SfntHorizontalMetric(lastAdvanceWidth, leftSideBearing);
        }

        for (int glyphIndex = numberOfHMetrics; glyphIndex < numGlyphs; glyphIndex++)
        {
            short leftSideBearing = reader.ReadInt16() ?? 0;
            metrics[glyphIndex] = new SfntHorizontalMetric(lastAdvanceWidth, leftSideBearing);
        }

        if (!reader.IsValid)
        {
            _logger.LogWarning(
                "Reading truncated 'hmtx' table: {NumberOfHMetrics} long entries and {SideBearingCount} trailing side bearings need more than the {ActualLength} bytes present; the metrics past the end took their defaults.",
                numberOfHMetrics,
                numGlyphs - numberOfHMetrics,
                data.Length);
        }

        return new SfntHmtx { Metrics = metrics };
    }

    /// <summary>
    /// Writes an "hmtx" table's binary content: a long (advance width + lsb) entry for the first
    /// <paramref name="numberOfHMetrics"/> glyphs, then a leading-side-bearing-only entry for every
    /// remaining glyph, per the table's own compression scheme. <paramref name="numberOfHMetrics"/>
    /// must be the same value written to <c>hhea.numberOfHMetrics</c>, and every glyph beyond it must
    /// share <see cref="SfntHmtx.Metrics"/>'s advance width at index <c>numberOfHMetrics - 1</c> -
    /// otherwise its (unwritten) advance width would silently read back wrong.
    /// </summary>
    public byte[] Write(SfntHmtx hmtx, ushort numberOfHMetrics)
    {
        if (hmtx == null)
        {
            throw new ArgumentNullException(nameof(hmtx));
        }

        SfntWriter writer = new();
        IReadOnlyList<SfntHorizontalMetric> metrics = hmtx.Metrics;

        int longEntryCount = Math.Min((int)numberOfHMetrics, metrics.Count);
        for (int glyphIndex = 0; glyphIndex < longEntryCount; glyphIndex++)
        {
            writer.WriteUInt16(metrics[glyphIndex].AdvanceWidth);
            writer.WriteInt16(metrics[glyphIndex].LeftSideBearing);
        }

        for (int glyphIndex = longEntryCount; glyphIndex < metrics.Count; glyphIndex++)
        {
            writer.WriteInt16(metrics[glyphIndex].LeftSideBearing);
        }

        return writer.ToArray();
    }
}
