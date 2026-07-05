using SkiaSharp;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Command modifier that applies a SrcIn color filter to tint recorded content
/// with a specific color. Used for uncolored tiling patterns and uncolored Type 3 glyphs
/// where the drawn shapes should adopt the current fill/stroke color at replay time.
/// </summary>
public sealed class UncoloredPaintModifier
{
    private readonly SKColorFilter _colorFilter;

    /// <summary>
    /// Creates a modifier that tints all paint with the specified color using SrcIn blending.
    /// </summary>
    /// <param name="color">The tint color to apply.</param>
    public UncoloredPaintModifier(in SKColor color)
    {
        // PDF uncolored semantics (concise):
        // final RGB = paint.rgb * paint.alpha * srcA
        // final A   = paint.alpha * srcA
        float pr = color.Red / 255f;
        float pg = color.Green / 255f;
        float pb = color.Blue / 255f;
        float pa = color.Alpha / 255f;

        float rMul = pr * pa;
        float gMul = pg * pa;
        float bMul = pb * pa;

        var matrix = new float[]
        {
            0f,
            0f,
            0f,
            rMul,
            0f,
            0f,
            0f,
            0f,
            gMul,
            0f,
            0f,
            0f,
            0f,
            bMul,
            0f,
            0f,
            0f,
            0f,
            pa,
            0f
        };

        _colorFilter = SKColorFilter.CreateColorMatrix(matrix);
    }

    /// <summary>
    /// Applies the SrcIn color filter to the paint. When the paint already has a color filter,
    /// the uncolored filter is composed on top of it.
    /// </summary>
    public void ModifyPaint(SKPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        paint.ColorFilter = (paint.ColorFilter != null)
            ? SKColorFilter.CreateCompose(_colorFilter, paint.ColorFilter)
            : _colorFilter;
    }

    /// <inheritdoc />
    public void Dispose() => _colorFilter?.Dispose();
}
