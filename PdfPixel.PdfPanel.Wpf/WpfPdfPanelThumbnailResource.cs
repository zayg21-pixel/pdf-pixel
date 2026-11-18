using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// The pixels of a page thumbnail of a <see cref="WpfPdfPanel"/> as a frozen RGB bitmap.
/// </summary>
public sealed class WpfPdfPanelThumbnailResource : IPdfPanelThumbnailResource
{
    private const double BitmapDpi = 96;

    internal WpfPdfPanelThumbnailResource(PdfImageSamples samples)
    {
        BitmapSource bitmap = BitmapSource.Create(samples.Width, samples.Height, BitmapDpi, BitmapDpi, PixelFormats.Rgb24, null, samples.Pixels.ToArray(), samples.RowBytes);
        bitmap.Freeze();
        Bitmap = bitmap;
    }

    /// <summary>
    /// Gets the bitmap of the thumbnail.
    /// </summary>
    public BitmapSource Bitmap { get; }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
