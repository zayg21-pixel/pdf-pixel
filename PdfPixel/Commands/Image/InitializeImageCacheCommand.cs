using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Creates and owns a <see cref="PdfCommandImageCache"/> for the lifetime of the command
/// recording. Sets the cache on the execution context during both Initialize and Execute
/// passes so that subsequent image tile commands can use it.
/// </summary>
internal sealed class InitializeImageCacheCommand : PdfCommand
{
    private readonly PdfCommandImageCache _imageCache = new();

    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.ImageCache = _imageCache;

    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.ImageCache = _imageCache;

    protected override void Dispose(bool disposing) => _imageCache.Dispose();
}
