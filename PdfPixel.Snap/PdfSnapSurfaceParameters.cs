using PdfPixel.Color.ColorSpace;
using PdfPixel.Imaging.Decoding;
using System;

namespace PdfPixel.Snap;

/// <summary>
/// What a <see cref="PdfSnapSurface"/> is created with and keeps for its lifetime, chosen by its owner.
/// </summary>
public sealed class PdfSnapSurfaceParameters
{
    /// <summary>
    /// Initializes parameters for surfaces whose root composites in <paramref name="rootColorSpace"/>, drawing their
    /// layers in <paramref name="memoryPool"/> and decoding images into <paramref name="imageCache"/>.
    /// </summary>
    /// <param name="rootColorSpace">The color space the root layer composites in.</param>
    /// <param name="memoryPool">The memory layers are drawn in.</param>
    /// <param name="imageCache">The cache images are decoded into.</param>
    public PdfSnapSurfaceParameters(PdfColorSpaceConverter rootColorSpace, PdfSnapMemoryPool memoryPool, IPdfImageCache imageCache)
    {
        RootColorSpace = rootColorSpace ?? throw new ArgumentNullException(nameof(rootColorSpace));
        MemoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
        ImageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
    }

    /// <summary>
    /// Gets the color space the root layer composites in.
    /// </summary>
    public PdfColorSpaceConverter RootColorSpace { get; }

    /// <summary>
    /// Gets the memory layers are drawn in.
    /// </summary>
    public PdfSnapMemoryPool MemoryPool { get; }

    /// <summary>
    /// Gets the cache images are decoded into.
    /// </summary>
    public IPdfImageCache ImageCache { get; }
}
