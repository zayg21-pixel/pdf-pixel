using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using System.Threading;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable thumbnail of a page: the page's thumbnail image (/Thumb) as a PNG file.
/// </summary>
public sealed class PdfThumbnailViewModel
{
    private readonly IPdfPage _page;
    private readonly object _documentLocker;
    private byte[]? _png;
    private bool _isPngDecoded;

    internal PdfThumbnailViewModel(IPdfPage page, int pageNumber, in PdfPanelPageInfo info, object documentLocker)
    {
        _page = page;
        _documentLocker = documentLocker;
        PageNumber = pageNumber;
        Info = info;
    }

    /// <summary>
    /// 1-based number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Label, crop box and rotation of the page.
    /// </summary>
    public PdfPanelPageInfo Info { get; }

    /// <summary>
    /// Size of the thumbnail image in pixels, or <see langword="null"/> when the page has no thumbnail.
    /// </summary>
    public PdfSize? Size
    {
        get
        {
            lock (_documentLocker)
            {
                PdfImage? thumbnail = _page.Thumbnail;
                if (thumbnail == null)
                {
                    return null;
                }

                return new PdfSize(thumbnail.Width, thumbnail.Height);
            }
        }
    }

    /// <summary>
    /// Thumbnail image as a PNG file, decoded the first time it is requested, or <see langword="null"/> when
    /// the page has no thumbnail.
    /// </summary>
    public byte[]? Png
    {
        get
        {
            if (!_isPngDecoded)
            {
                _png = DecodePng();
                _isPngDecoded = true;
            }

            return _png;
        }
    }

    private byte[]? DecodePng()
    {
        lock (_documentLocker)
        {
            PdfImage? thumbnail = _page.Thumbnail;
            if (thumbnail == null)
            {
                return null;
            }

            PdfCancellationExecutionObserver observer = new(CancellationToken.None);

            return thumbnail.DecodePng(PdfImageDecodingParameters.Default, observer);
        }
    }
}
