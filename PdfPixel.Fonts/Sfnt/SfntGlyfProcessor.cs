using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "glyf" table. Like "hmtx", it is not self-describing:
/// <see cref="Read"/> needs "loca" as an input, since "glyf" is simply a concatenation of
/// variable-length glyph records with no directory of its own. Delegates per-glyph outline extraction
/// and repacking to <see cref="SfntGlyphEvaluator"/>.
/// </summary>
public class SfntGlyfProcessor
{
    private readonly ILogger<SfntGlyfProcessor> _logger;
    private readonly SfntGlyphEvaluator _evaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyfProcessor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during evaluation.</param>
    public SfntGlyfProcessor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SfntGlyfProcessor>();
        _evaluator = new SfntGlyphEvaluator(loggerFactory.CreateLogger<SfntGlyphEvaluator>());
    }

    /// <summary>
    /// Parses a "glyf" table from its raw content bytes, slicing out each glyph via
    /// <paramref name="loca"/> and evaluating it. A glyph whose slice is empty has no outline (e.g.
    /// space) and is left null; a glyph whose slice is out of bounds or malformed is also left null,
    /// with a logged warning, rather than failing the whole table.
    /// </summary>
    /// <param name="data">The "glyf" table's raw content bytes.</param>
    /// <param name="loca">This font's parsed "loca" table.</param>
    /// <param name="matrix">Transform applied to every point of each glyph's resulting path.</param>
    public SfntGlyf Read(in ReadOnlyMemory<byte> data, SfntLoca loca, in PdfFontMatrix matrix)
    {
        if (loca == null)
        {
            throw new ArgumentNullException(nameof(loca));
        }

        IReadOnlyList<uint> offsets = loca.Offsets;
        int numGlyphs = Math.Max(0, offsets.Count - 1);
        var rawGlyphs = new ReadOnlyMemory<byte>[numGlyphs];

        for (int glyphId = 0; glyphId < numGlyphs; glyphId++)
        {
            uint startOffset = offsets[glyphId];
            uint endOffset = offsets[glyphId + 1];
            if (endOffset <= startOffset)
            {
                continue; // No outline for this glyph.
            }

            if (endOffset > (uint)data.Length)
            {
                _logger.LogWarning(
                    "Failed to read glyph {GlyphId}: offset range {StartOffset}-{EndOffset} exceeds 'glyf' table length {ActualLength}.",
                    glyphId,
                    startOffset,
                    endOffset,
                    data.Length);
                continue;
            }

            rawGlyphs[glyphId] = data.Slice((int)startOffset, (int)(endOffset - startOffset));
        }

        var glyphs = new SfntGlyphCharacter?[numGlyphs];
        for (int glyphId = 0; glyphId < numGlyphs; glyphId++)
        {
            if (rawGlyphs[glyphId].Length == 0)
            {
                continue;
            }

            glyphs[glyphId] = _evaluator.Evaluate(rawGlyphs[glyphId], rawGlyphs, matrix);
        }

        return new SfntGlyf { Glyphs = glyphs };
    }

    /// <summary>
    /// Writes a "glyf" table's binary content by concatenating every glyph's already-repacked bytes,
    /// along with the "loca" table that indexes them. Each glyph is padded to an even byte boundary,
    /// since a short-format "loca" can only represent even offsets.
    /// </summary>
    public SfntGlyfWriteResult Write(SfntGlyf glyf)
    {
        if (glyf == null)
        {
            throw new ArgumentNullException(nameof(glyf));
        }

        SfntWriter writer = new();
        var offsets = new uint[glyf.Glyphs.Count + 1];

        for (int glyphId = 0; glyphId < glyf.Glyphs.Count; glyphId++)
        {
            offsets[glyphId] = (uint)writer.Length;

            SfntGlyphCharacter? glyph = glyf.Glyphs[glyphId];
            if (glyph != null)
            {
                writer.WriteBytes(glyph.GlyphData.Span);
                if ((writer.Length & 1) != 0)
                {
                    writer.WriteByte(0);
                }
            }
        }

        offsets[glyf.Glyphs.Count] = (uint)writer.Length;

        return new SfntGlyfWriteResult(writer.ToArray(), new SfntLoca { Offsets = offsets });
    }
}
