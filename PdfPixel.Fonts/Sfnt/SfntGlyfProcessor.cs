using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Resolves individual glyphs from the SFNT "glyf" table on demand, caching each result on
/// <see cref="SfntGlyf"/>, and writes a full "glyf" table back to bytes. Like "hmtx", "glyf" is not
/// self-describing: a glyph's byte range comes from "loca". Delegates per-glyph outline extraction
/// and repacking to <see cref="SfntGlyphEvaluator"/>.
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
    /// Resolves a single glyph, returning the cached result if <paramref name="glyf"/> already has one
    /// for <paramref name="gid"/>. Otherwise fetches the glyph's raw bytes via <paramref name="source"/>
    /// and <see cref="SfntGlyf.Loca"/>, evaluates it, caches the result on <paramref name="glyf"/>, and
    /// returns it.
    /// </summary>
    /// <param name="glyf">The font's "glyf" table cache.</param>
    /// <param name="gid">The glyph ID to resolve.</param>
    /// <param name="source">The stream and table range to read this font's "glyf" table from.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public SfntGlyphCharacter? ResolveGlyph(SfntGlyf glyf, int gid, in SfntGlyfSource source, in PdfFontMatrix matrix)
    {
        if (glyf == null)
        {
            throw new ArgumentNullException(nameof(glyf));
        }

        if (glyf.Contains(gid))
        {
            return glyf.Get(gid);
        }

        ReadOnlyMemory<byte> glyphData = FetchRawGlyph(gid, glyf.Loca, source);
        SfntGlyphCharacter? glyph = _evaluator.Evaluate(glyphData, this, glyf.Loca, source, matrix);

        glyf.Set(gid, glyph);
        return glyph;
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
    /// Writes a full "glyf" table's binary content, resolving (and caching) every glyph that isn't
    /// already cached on <paramref name="glyf"/>, along with the "loca" table that indexes them. Each
    /// glyph is padded to an even byte boundary, since a short-format "loca" can only represent even
    /// offsets.
    /// </summary>
    public SfntGlyfWriteResult Write(SfntGlyf glyf, in SfntGlyfSource source, in PdfFontMatrix matrix)
    {
        if (glyf == null)
        {
            throw new ArgumentNullException(nameof(glyf));
        }

        SfntWriter writer = new();
        int numGlyphs = glyf.NumGlyphs;
        var ranges = new SfntGlyphRange[numGlyphs];

        for (int gid = 0; gid < numGlyphs; gid++)
        {
            var startOffset = (uint)writer.Length;

            SfntGlyphCharacter? glyph = ResolveGlyph(glyf, gid, source, matrix);
            if (glyph != null)
            {
                writer.WriteBytes(glyph.GlyphData.Span);
                if ((writer.Length & 1) != 0)
                {
                    writer.WriteByte(0);
                }
            }

            ranges[gid] = new SfntGlyphRange(startOffset, (uint)writer.Length - startOffset);
        }

        return new SfntGlyfWriteResult(writer.ToArray(), new SfntLoca { Ranges = ranges });
    }
}
