using PdfPixel.Imaging.Processing.Pipeline;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// A finished thumbnail request of the scheduler, on its way to the panel: its page, the observer of the request, and
/// the thumbnail's pixels when it was produced.
/// </summary>
internal sealed class PdfPageRenderedThumbnail
{
    /// <summary>
    /// Initializes the finished request of <paramref name="observer"/> for the thumbnail of
    /// <paramref name="pageNumber"/>.
    /// </summary>
    public PdfPageRenderedThumbnail(int pageNumber, IPdfCancellableExecutionObserver observer, PdfImageSamples? samples)
    {
        PageNumber = pageNumber;
        Observer = observer;
        Samples = samples;
    }

    /// <summary>
    /// Gets the 1-based number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the observer of the request the thumbnail was produced for.
    /// </summary>
    public IPdfCancellableExecutionObserver Observer { get; }

    /// <summary>
    /// Gets the thumbnail as opaque 8-bit RGB samples, or <see langword="null"/> when it could not be produced.
    /// </summary>
    public PdfImageSamples? Samples { get; }
}
