using PdfPixel.Color.Paint;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Draws shaped text at the origin by building one or more text blobs from pre-shaped glyphs.
/// Resolves each glyph's <see cref="SKTypeface"/> lazily at execution time via
/// <see cref="PdfCommandExecutionContext.Cache"/>, so the same native typeface is reused across every
/// command that draws with it during a replay.
/// </summary>
public sealed class DrawShapedTextCommand : PdfCommand, IMatrixCommand, IPaintCommand
{
    private const float ScaleTolerancePercent = 0.01f; // 1%

    private readonly bool _isSimpleCase;

    /// <summary>
    /// Initializes the command with the given matrix, shaped glyphs and paint. Checks up front
    /// whether every glyph shares the same typeface and (within <see cref="ScaleTolerancePercent"/>)
    /// scale, so <see cref="Execute"/> can take the single-span fast path instead of grouping glyphs
    /// into spans.
    /// </summary>
    public DrawShapedTextCommand(in PdfMatrix matrix, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfPaint paint)
    {
        Matrix = matrix;
        ShapingResult = shapingResult;
        Paint = paint;
        _isSimpleCase = IsSimpleCase(shapingResult);
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the pre-shaped glyphs drawn by this command.
    /// </summary>
    public ReadOnlyMemory<ShapedGlyph> ShapingResult { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        ReadOnlySpan<ShapedGlyph> glyphs = ShapingResult.Span;
        if (glyphs.Length == 0)
        {
            return;
        }

        using SKPaint paint = Paint.ToSkiaPaint();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        CommandHelpers.ApplyModifiers(paint, executionContext);

        SKCanvas canvas = executionContext.Canvas;
        canvas.Save();
        canvas.Concat(Matrix.ToSkMatrix());

        if (_isSimpleCase)
        {
            DrawSpan(executionContext, canvas, paint, antialias, glyphs, glyphs[0].CharacterInfo.Typeface, glyphs[0].Scale);
        }
        else
        {
            DrawSpans(executionContext, canvas, paint, antialias, glyphs);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Groups glyphs into runs of matching typeface and (within <see cref="ScaleTolerancePercent"/>)
    /// scale, drawing each run with its own <see cref="SKFont"/>.
    /// </summary>
    private static void DrawSpans(PdfCommandExecutionContext executionContext, SKCanvas canvas, SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> glyphs)
    {
        int spanStart = 0;
        IPdfTypeface currentTypeface = glyphs[0].CharacterInfo.Typeface;
        float currentScale = glyphs[0].Scale;

        for (int i = 1; i < glyphs.Length; i++)
        {
            ShapedGlyph glyph = glyphs[i];
            IPdfTypeface typeface = glyph.CharacterInfo.Typeface;

            if (typeface != currentTypeface || Math.Abs(glyph.Scale - currentScale) / currentScale >= ScaleTolerancePercent)
            {
                DrawSpan(executionContext, canvas, paint, antialias, glyphs.Slice(spanStart, i - spanStart), currentTypeface, currentScale);

                spanStart = i;
                currentTypeface = typeface;
                currentScale = glyph.Scale;
            }
        }

        DrawSpan(executionContext, canvas, paint, antialias, glyphs.Slice(spanStart), currentTypeface, currentScale);
    }

    private static void DrawSpan(PdfCommandExecutionContext executionContext, SKCanvas canvas, SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> span, IPdfTypeface typeface, float scale)
    {
        SKTypeface skTypeface = CommandHelpers.GetOrCreateSkTypeface(executionContext, typeface);
        using SKFont font = CreateTextFont(skTypeface, scale);
        CommandHelpers.ApplyAntialias(font, antialias);

        using SKTextBlob? blob = BuildTextBlob(span, font);

        if (blob != null)
        {
            canvas.DrawText(blob, 0f, 0f, paint);
        }
    }

    private static SKTextBlob? BuildTextBlob(in ReadOnlySpan<ShapedGlyph> shapingResult, SKFont font)
    {
        // Pre-count drawable glyphs (gid != 0) while computing positions using full advance including skipped glyphs.
        int drawableCount = 0;
        for (int i = 0; i < shapingResult.Length; i++)
        {
            if (shapingResult[i].GlyphId != 0)
            {
                drawableCount++;
            }
        }

        using SKTextBlobBuilder builder = new();
        SKPositionedRunBuffer run = builder.AllocatePositionedRun(font, drawableCount);
        System.Span<ushort> glyphSpan = run.Glyphs;
        System.Span<SKPoint> positionSpan = run.Positions;

        int drawIndex = 0;
        for (int index = 0; index < shapingResult.Length; index++)
        {
            ShapedGlyph shapedGlyph = shapingResult[index];
            // Record position regardless to advance subsequent glyphs.
            if (shapedGlyph.GlyphId != 0)
            {
                glyphSpan[drawIndex] = (ushort)shapedGlyph.GlyphId;
                positionSpan[drawIndex] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                drawIndex++;
            }
        }

        return builder.Build();
    }

    private static SKFont CreateTextFont(SKTypeface skTypeface, float scale)
    {
        SKFont font = new()
        {
            Typeface = skTypeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Slight,
            ScaleX = scale
        };

        return font;
    }

    private static bool IsSimpleCase(in ReadOnlyMemory<ShapedGlyph> shapingResult)
    {
        if (shapingResult.IsEmpty)
        {
            return true;
        }

        ReadOnlySpan<ShapedGlyph> glyphs = shapingResult.Span;
        IPdfTypeface typeface = glyphs[0].CharacterInfo.Typeface;
        float scale = glyphs[0].Scale;

        for (int i = 1; i < glyphs.Length; i++)
        {
            if (glyphs[i].CharacterInfo.Typeface != typeface || Math.Abs(glyphs[i].Scale - scale) / scale >= ScaleTolerancePercent)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string chars = string.Join(" ", ShapingResult.Span.ToArray().Select(glyph => $"{glyph.CharacterInfo.Unicode}/{glyph.GlyphId}"));
        return $"{nameof(DrawShapedTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{chars}\"";
    }
}
