using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Thumbnails;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable thumbnail of a page as a PNG file, produced and kept while it is visible.
/// </summary>
public sealed class PdfThumbnailViewModel : INotifyPropertyChanged
{
    private readonly PdfPanelThumbnails _thumbnails;
    private bool _isVisible;
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
    /// Thumbnail as a PNG file, or <see langword="null"/> when it is not visible or not ready.
    /// </summary>
    public byte[]? Png => _isVisible ? _thumbnails.GetPng(PageNumber) : null;

    /// <summary>
    /// Whether the thumbnail is shown by the view: setting it produces the thumbnail and keeps it, clearing it lets it be evicted.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;

        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;

            if (value)
            {
                _thumbnails.Request(PageNumber, pin: true);
            }
            else
            {
                _thumbnails.Release(PageNumber);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));
        }
    }

    /// <summary>
    /// Reports <see cref="Png"/> of the thumbnail that became ready while it is visible.
    /// </summary>
    internal void OnThumbnailReady()
    {
        if (_isVisible)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));
        }
    }

    /// <summary>
    /// Reports the dropped thumbnail.
    /// </summary>
    internal void OnCleared()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Png)));
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
