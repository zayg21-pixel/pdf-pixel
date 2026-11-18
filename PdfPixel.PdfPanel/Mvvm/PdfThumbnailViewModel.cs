using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Thumbnails;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable thumbnail of a page as the host's pixels, shown while it is visible; the host requests the thumbnails of
/// the visible items.
/// </summary>
public sealed class PdfThumbnailViewModel : INotifyPropertyChanged
{
    private readonly PdfThumbnailsViewModel _owner;
    private readonly PdfPanelThumbnails _thumbnails;
    private bool _isVisible;
    private int _reportedRotation;

    internal PdfThumbnailViewModel(PdfThumbnailsViewModel owner, PdfPanelThumbnails thumbnails, PdfPanelPage page)
    {
        _owner = owner;
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
    /// Host's pixels of the thumbnail, or <see langword="null"/> when it is not visible or not ready.
    /// </summary>
    public IPdfPanelThumbnailResource? Resource => _isVisible ? _thumbnails.Get(PageNumber) : null;

    /// <summary>
    /// Whether the thumbnail is shown by the view; changing it raises
    /// <see cref="PdfThumbnailsViewModel.VisibleItemsChanged"/>.
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

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Resource)));
            _owner.OnItemVisibilityChanged();
        }
    }

    /// <summary>
    /// Reports <see cref="Resource"/> of the thumbnail that was produced while it is visible.
    /// </summary>
    internal void OnThumbnailReady()
    {
        if (_isVisible)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Resource)));
        }
    }

    /// <summary>
    /// Reports the dropped thumbnail.
    /// </summary>
    internal void OnCleared()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Resource)));
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
