using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// A no-op command modifier that leaves paints unchanged.
/// </summary>
public sealed class DefaultPdfCommandModifier : IPdfCommandModifier
{
    /// <inheritdoc />
    public void ModifyPaint(SKPaint paint)
    {
        // No modifications by default
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
