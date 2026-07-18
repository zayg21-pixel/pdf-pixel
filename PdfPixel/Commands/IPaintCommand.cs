using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Exposes the paint a command applies when drawing.
/// </summary>
public interface IPaintCommand
{
    /// <summary>
    /// Gets the paint this command applies, or <see langword="null"/> when the command draws without a paint override.
    /// </summary>
    SKPaint? Paint { get; }
}
