using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// The thumbnail of a page in the forms produced so far: a PNG file, opaque 8-bit RGB samples, or both.
/// </summary>
internal sealed class PdfPageThumbnail
{
    /// <summary>
    /// Gets or sets the thumbnail as a PNG file, or <see langword="null"/> when it was not produced in that form.
    /// </summary>
    public byte[]? Png { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail as opaque 8-bit RGB samples, or <see langword="null"/> when it was not produced in
    /// that form.
    /// </summary>
    public PdfImageSamples? Samples { get; set; }

    /// <summary>
    /// Gets the forms the thumbnail was produced in.
    /// </summary>
    public PdfPanelThumbnailFormats Formats
    {
        get
        {
            var formats = PdfPanelThumbnailFormats.None;

            if (Png != null)
            {
                formats |= PdfPanelThumbnailFormats.Png;
            }

            if (Samples != null)
            {
                formats |= PdfPanelThumbnailFormats.Samples;
            }

            return formats;
        }
    }
}
