using SkiaSharp;

namespace PdfPixel.Commands;

// TODO: [HIGH] this might be a big mistake to use it like this
/// <summary>
/// Command modifier that overrides the blend mode on every paint during command replay.
/// </summary>
internal sealed class BlendModePaintModifier : IPdfCommandModifier
{
    private readonly SKBlendMode _blendMode;

    /// <summary>
    /// Creates a modifier that sets the specified blend mode on all paints.
    /// </summary>
    /// <param name="blendMode">The blend mode to apply.</param>
    public BlendModePaintModifier(SKBlendMode blendMode) => _blendMode = blendMode;

    /// <inheritdoc />
    public void ModifyPaint(SKPaint paint) => paint.BlendMode = _blendMode;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
