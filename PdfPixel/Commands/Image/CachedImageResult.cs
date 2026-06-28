using SkiaSharp;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Result of a cache lookup — either an atlased region or a standalone image.
/// </summary>
internal readonly struct CachedImageResult
{
    public CachedImageResult(SKImage image, SKRectI sourceRegion, bool isAtlased)
    {
        Image = image;
        SourceRegion = sourceRegion;
        IsAtlased = isAtlased;
    }

    /// <summary>
    /// The atlas image (when atlased) or the standalone image.
    /// </summary>
    public SKImage Image { get; }

    /// <summary>
    /// Source region within <see cref="Image"/>. Only meaningful when <see cref="IsAtlased"/> is true.
    /// </summary>
    public SKRectI SourceRegion { get; }

    /// <summary>
    /// True when this image was packed into an atlas; false when it was too large and stored standalone.
    /// </summary>
    public bool IsAtlased { get; }
}
