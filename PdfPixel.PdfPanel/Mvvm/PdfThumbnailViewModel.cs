using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Thumbnails;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable thumbnail of a page as a PNG file, produced while it is requested.
/// </summary>
public sealed class PdfThumbnailViewModel : INotifyPropertyChanged
{
    private readonly PdfPanelThumbnails _thumbnails;
    private bool _isRequested;

    internal PdfThumbnailViewModel(PdfPanelThumbnails thumbnails, int pageNumber, in PdfPanelPageInfo info)
    {
        _thumbnails = thumbnails;
        PageNumber = pageNumber;
        Info = info;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 1-based number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Label, crop box and rotation of the page.
    /// </summary>
    public PdfPanelPageInfo Info { get; }

    /// <summary>
    /// Size of the thumbnail in pixels, or <see langword="null"/> when the page has none.
    /// </summary>
    public PdfSize? Size => _thumbnails.GetSize(PageNumber);

    /// <summary>
    /// Thumbnail as a PNG file, or <see langword="null"/> when it is not ready.
    /// </summary>
    public byte[]? Png => _thumbnails.GetPng(PageNumber);

    /// <summary>
    /// Whether the thumbnail is requested: setting it starts producing the thumbnail, clearing it cancels producing.
    /// </summary>
    public bool IsRequested
    {
        get => _isRequested;

        set
        {
            if (_isRequested == value)
            {
                return;
            }

            _isRequested = value;

            if (value)
            {
                _thumbnails.Request(PageNumber);
            }
            else
            {
                _thumbnails.Cancel(PageNumber);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRequested)));
        }
    }

    /// <summary>
    /// Reports the thumbnail that became ready.
    /// </summary>
    internal void OnThumbnailReady() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));

    /// <summary>
    /// Reports the dropped thumbnail and requests it again while it is requested.
    /// </summary>
    internal void OnCleared()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));

        if (_isRequested)
        {
            _thumbnails.Request(PageNumber);
        }
    }
}
