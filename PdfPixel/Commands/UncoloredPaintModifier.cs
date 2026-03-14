using System;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Command modifier that applies a SrcIn color filter to tint recorded content
/// with a specific color. Used for uncolored tiling patterns and uncolored Type 3 glyphs
/// where the drawn shapes should adopt the current fill/stroke color at replay time.
/// </summary>
internal sealed class UncoloredPaintModifier : IPdfCommandModifier
{
    private readonly SKColorFilter _colorFilter;

    /// <summary>
    /// Creates a modifier that tints all paint with the specified color using SrcIn blending.
    /// </summary>
    /// <param name="color">The tint color to apply.</param>
    public UncoloredPaintModifier(SKColor color)
    {
        _colorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn);
    }

    /// <summary>
    /// Applies the SrcIn color filter to the paint. When the paint already has a color filter,
    /// the uncolored filter is composed on top of it.
    /// </summary>
    public void ModifyPaint(SKPaint paint)
    {
        paint.ColorFilter = paint.ColorFilter != null
            ? SKColorFilter.CreateCompose(_colorFilter, paint.ColorFilter)
            : _colorFilter;
    }

    public void Dispose()
    {
        _colorFilter?.Dispose();
    }
}
