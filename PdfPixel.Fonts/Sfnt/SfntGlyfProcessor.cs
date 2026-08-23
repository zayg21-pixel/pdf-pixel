using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads glyphs out of the SFNT "glyf" table, which - like "hmtx" - is not self-describing: a glyph's
/// byte range comes from "loca". Every glyph is evaluated to a <see cref="GlyphOutline"/> by
/// <see cref="SfntGlyphEvaluator"/> first; writing a full "glyf" table then hands each outline to
/// <see cref="SfntGlyphRepacker"/>, while resolving a single glyph's path hands it to
/// <see cref="SfntGlyphPathEmitter"/>. Only paths are cached - a repack keeps nothing, since it reads
/// every glyph exactly once.
/// </summary>
public class SfntGlyfProcessor
{
    private readonly SfntGlyphEvaluator _evaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyfProcessor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during evaluation.</param>
    public SfntGlyfProcessor(ILoggerFactory loggerFactory) => _evaluator = new SfntGlyphEvaluator(loggerFactory.CreateLogger<SfntGlyphEvaluator>());

    /// <summary>
    /// Resolves a single glyph's path, returning the cached one if <paramref name="glyf"/> already
    /// holds it. Otherwise evaluates the glyph, emits its path, caches it on <paramref name="glyf"/>,
    /// and returns it.
    /// </summary>
    /// <param name="glyf">The font's "glyf" table cache.</param>
    /// <param name="gid">The glyph ID to resolve.</param>
    /// <param name="source">The stream and table range to read this font's "glyf" table from.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public ReadOnlyMemory<byte> ResolvePath(SfntGlyf glyf, int gid, in SfntGlyfSource source, in PdfFontMatrix matrix)
    {
        if (glyf == null)
        {
            throw new ArgumentNullException(nameof(glyf));
        }

        ReadOnlyMemory<byte> cachedPath = glyf.GetPath(gid);
        if (!cachedPath.IsEmpty)
        {
            return cachedPath;
        }

        ReadOnlyMemory<byte> glyphData = FetchRawGlyph(gid, glyf.Loca, source);
        GlyphOutline? outline = _evaluator.Evaluate(glyphData, this, glyf.Loca, source);
        if (outline == null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        ReadOnlyMemory<byte> path = SfntGlyphPathEmitter.Emit(outline, matrix);
        glyf.SetPath(gid, path);

        return path;
    }

    /// <summary>
    /// Fetches a single glyph's raw bytes out of "glyf" via "loca", reading only that glyph's byte
    /// range from the source stream. Returns an empty span if the glyph id is out of range or the
    /// glyph has no outline (e.g. space).
    /// </summary>
    internal ReadOnlyMemory<byte> FetchRawGlyph(int gid, SfntLoca loca, in SfntGlyfSource source)
    {
        IReadOnlyList<SfntGlyphRange> ranges = loca.Ranges;
        if (gid < 0 || gid >= ranges.Count)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        SfntGlyphRange range = ranges[gid];
        if (range.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return source.Stream.GetMemory(source.GlyfRecord.Offset + (int)range.Offset, (int)range.Length);
    }

    /// <summary>
    /// Writes a "glyf" table's binary content, along with the "loca" table that indexes them. Without
    /// <paramref name="glyphOrder"/> every glyph keeps the glyph id it already had, so a composite
    /// glyph is written back as it stands - what its components reference still resolves - and only a
    /// simple glyph is decoded and repacked. With one, the table holds only the glyphs it names, at the
    /// ids it names them, and every glyph is written as a simple glyph with a composite's components
    /// resolved into its own outline. Each glyph is padded to an even byte boundary, since a
    /// short-format "loca" can only represent even offsets.
    /// </summary>
    /// <param name="glyf">The font's "glyf" table cache, whose "loca" indexes the source glyphs.</param>
    /// <param name="source">The stream and table range to read this font's "glyf" table from.</param>
    /// <param name="glyphOrder">The source glyph ids to write, in the order the result numbers them. Null writes every glyph at the id it already had.</param>
    public SfntGlyfWriteResult Write(SfntGlyf glyf, in SfntGlyfSource source, IReadOnlyList<ushort>? glyphOrder = null)
    {
        if (glyf == null)
        {
            throw new ArgumentNullException(nameof(glyf));
        }

        int numGlyphs = glyphOrder?.Count ?? glyf.NumGlyphs;

        // A repacked simple glyph is its source's size and a composite is copied at its own, so the
        // source table plus a pad byte per glyph writes the whole table without growing.
        SfntWriter writer = new(source.GlyfRecord.Length + numGlyphs);
        SfntGlyphRepacker repacker = new();
        var ranges = new SfntGlyphRange[numGlyphs];

        for (int gid = 0; gid < numGlyphs; gid++)
        {
            var startOffset = (uint)writer.Length;

            int sourceGid = (glyphOrder != null) ? glyphOrder[gid] : gid;
            ReadOnlyMemory<byte> glyphData = FetchRawGlyph(sourceGid, glyf.Loca, source);

            if (glyphOrder == null && IsComposite(glyphData.Span))
            {
                writer.WriteBytes(glyphData.Span);
            }
            else if (glyphData.Length > 0)
            {
                GlyphOutline? outline = _evaluator.Evaluate(glyphData, this, glyf.Loca, source);

                if (outline != null)
                {
                    repacker.Repack(writer, outline);
                }
            }

            // Every glyph ends on an even boundary, so an odd length here is this glyph's alone.
            if ((writer.Length & 1) != 0)
            {
                writer.WriteByte(0);
            }

            ranges[gid] = new SfntGlyphRange(startOffset, (uint)writer.Length - startOffset);
        }

        return new SfntGlyfWriteResult(writer.Detach(), new SfntLoca { Ranges = ranges });
    }

    /// <summary>
    /// Reports whether a glyph's raw bytes describe a composite glyph, which a negative contour count
    /// in the glyph header marks.
    /// </summary>
    private static bool IsComposite(in ReadOnlySpan<byte> glyphData)
        => glyphData.Length >= 2 && (short)((glyphData[0] << 8) | glyphData[1]) < 0;
}
