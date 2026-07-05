using PdfPixel.Commands.Image;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Creates and owns a <see cref="PdfCommandImageCache"/> for the lifetime of the command
/// recording. Sets the cache on the execution context during both Initialize and Execute
/// passes so that subsequent image tile commands can use it.
/// </summary>
public sealed class InitializeImageCacheCommand : PdfCommand
{
    private readonly PdfCommandImageCache _imageCache;

    /// <summary>
    /// Initializes the command with the atlas packing thresholds from <see cref="PdfPixel.Models.PdfRenderingParameters"/>.
    /// </summary>
    /// <param name="maxAtlasedImageSize">Maximum width/height, in pixels, for an image to be eligible for atlas packing.</param>
    /// <param name="atlasPageSize">Size, in pixels, of each square atlas page.</param>
    public InitializeImageCacheCommand(int maxAtlasedImageSize, int atlasPageSize)
        => _imageCache = new PdfCommandImageCache(maxAtlasedImageSize, atlasPageSize);

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Initialize(PdfCommandExecutionContext executionContext)
        => executionContext.ImageCache = _imageCache;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
        => executionContext.ImageCache = _imageCache;

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _imageCache.Dispose();
}
