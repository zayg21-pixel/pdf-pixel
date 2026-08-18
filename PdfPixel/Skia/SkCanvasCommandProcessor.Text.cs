using PdfPixel.Skia.Converters;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Geometry;
using PdfPixel.Text;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private const float DrawShapedTextScaleTolerancePercent = 0.01f; // 1%

    private void ExecuteDrawShapedText(DrawShapedTextCommand command)
    {
        ReadOnlySpan<ShapedGlyph> glyphs = command.ShapingResult.Span;
        if (glyphs.Length == 0)
        {
            return;
        }

        using SKPaint paint = command.Paint.ToSkiaPaint();
        bool antialias = _executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(command.Matrix.ToSkMatrix());

        DrawShapedTextSpans(paint, antialias, glyphs);

        _canvas.Restore();
    }

    private void ExecuteTextCharacters(TextCharactersCommand command)
    {
        PdfMatrix matrix = _executionContext.Frames.TotalMatrix.PreConcat(command.Matrix);

        List<PdfCharacter> mapped = new(command.Characters.Length);

        for (int i = 0; i < command.Characters.Length; i++)
        {
            PdfRectangle pageRect = matrix.MapRect(command.Characters[i].BoundingBox);
            if (pageRect.Width != 0)
            {
                mapped.Add(new PdfCharacter(command.Characters[i].Text, pageRect));
            }
        }

        if (mapped.Count > 0)
        {
            _executionContext.MarkedContent.AppendCharacters(mapped);
        }
    }

    private void DrawShapedTextSpans(SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> glyphs)
    {
        int spanStart = 0;
        IPdfTypeface currentTypeface = glyphs[0].CharacterInfo.Typeface;
        float currentScale = glyphs[0].Scale;

        for (int i = 1; i < glyphs.Length; i++)
        {
            ShapedGlyph glyph = glyphs[i];
            IPdfTypeface typeface = glyph.CharacterInfo.Typeface;

            if (typeface != currentTypeface || Math.Abs(glyph.Scale - currentScale) / currentScale >= DrawShapedTextScaleTolerancePercent)
            {
                DrawShapedTextSpan(paint, antialias, glyphs.Slice(spanStart, i - spanStart), currentTypeface, currentScale);

                spanStart = i;
                currentTypeface = typeface;
                currentScale = glyph.Scale;
            }
        }

        DrawShapedTextSpan(paint, antialias, glyphs.Slice(spanStart), currentTypeface, currentScale);
    }

    private void DrawShapedTextSpan(SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> span, IPdfTypeface typeface, float scale)
    {
        SKTypeface skTypeface = SkiaCommandUtilities.GetOrCreateSkTypeface(_executionContext, _fontSubstitutor, typeface);
        using SKFont font = CreateShapedTextFont(skTypeface, scale);
        SkiaCommandUtilities.ApplyAntialias(font, antialias);

        using SKTextBlob? blob = BuildShapedTextBlob(span, font);

        if (blob != null)
        {
            _canvas.DrawText(blob, 0f, 0f, paint);
        }
    }

    private static SKTextBlob? BuildShapedTextBlob(in ReadOnlySpan<ShapedGlyph> shapingResult, SKFont font)
    {
        int drawableCount = 0;
        for (int i = 0; i < shapingResult.Length; i++)
        {
            if (shapingResult[i].GlyphId != null)
            {
                drawableCount++;
            }
        }

        using SKTextBlobBuilder builder = new();
        SKPositionedRunBuffer run = builder.AllocatePositionedRun(font, drawableCount);
        Span<ushort> glyphSpan = run.Glyphs;
        Span<SKPoint> positionSpan = run.Positions;

        int drawIndex = 0;
        for (int index = 0; index < shapingResult.Length; index++)
        {
            ShapedGlyph shapedGlyph = shapingResult[index];
            if (shapedGlyph.GlyphId != null)
            {
                glyphSpan[drawIndex] = shapedGlyph.GlyphId.Value;
                positionSpan[drawIndex] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                drawIndex++;
            }
        }

        return builder.Build();
    }

    private static SKFont CreateShapedTextFont(SKTypeface skTypeface, float scale)
    {
        SKFont font = new()
        {
            Typeface = skTypeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            ScaleX = scale
        };

        return font;
    }
}
