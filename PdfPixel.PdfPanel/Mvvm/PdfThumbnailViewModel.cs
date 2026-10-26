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
    private byte[]? _png;
    private int _reportedRotation;

    internal PdfThumbnailViewModel(PdfPanelThumbnails thumbnails, PdfPanelPage page)
    {
        _thumbnails = thumbnails;
        Page = page;
        _reportedRotation = page.Rotation;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 1-based number of the page.
    /// </summary>
    public int PageNumber => Page.PageNumber;

    /// <summary>
    /// Page the thumbnail shows.
    /// </summary>
    public PdfPanelPage Page { get; }

    /// <summary>
    /// Clockwise rotation of the page in degrees.
    /// </summary>
    public int Rotation => Page.Rotation;

    /// <summary>
    /// Size of the thumbnail in pixels, or <see langword="null"/> when the page has none.
    /// </summary>
    public PdfSize? Size => _thumbnails.GetSize(PageNumber);

    /// <summary>
    /// Thumbnail as a PNG file, or <see langword="null"/> when it is not ready.
    /// </summary>
    public byte[]? Png => _png;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRequested)));

            if (value)
            {
                byte[]? png = _thumbnails.GetPng(PageNumber);

                if (png == null)
                {
                    _thumbnails.Request(PageNumber);
                }
                else if (!ReferenceEquals(png, _png))
                {
                    _png = png;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));
                }
            }
            else
            {
                _thumbnails.Cancel(PageNumber);
            }
        }
    }

    /// <summary>
    /// Keeps the thumbnail that became ready while it is requested.
    /// </summary>
    internal void OnThumbnailReady(byte[] png)
    {
        if (!_isRequested)
        {
            return;
        }

        _png = png;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));
    }

    /// <summary>
    /// Reports the dropped thumbnail and requests it again while it is requested.
    /// </summary>
    internal void OnCleared()
    {
        _png = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));

        if (_isRequested)
        {
            _thumbnails.Request(PageNumber);
        }
    }

    /// <summary>
    /// Reports <see cref="Rotation"/> when it changed since it was last reported.
    /// </summary>
    internal void Synchronize()
    {
        if (Rotation == _reportedRotation)
        {
            return;
        }

        _reportedRotation = Rotation;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rotation)));
    }
}
