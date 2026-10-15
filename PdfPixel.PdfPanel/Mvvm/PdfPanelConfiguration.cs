using PdfPixel.Color;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Settings of a panel, applied to a <see cref="PdfPanelContext"/> with <see cref="ApplyConfiguration"/>.
/// </summary>
public sealed class PdfPanelConfiguration : INotifyPropertyChanged
{
    private float _minScale = 0.1f;
    private float _maxScale = 10f;
    private float _zoomStep = 0.1f;
    private float _pageGap = 10f;
    private PdfRectangle _layoutPadding = new(10f, 10f, 10f, 10f);
    private PdfColor _panelBackgroundColor = new(211f / 255f, 211f / 255f, 211f / 255f, 1f);
    private float _pageCornerRadius;
    private bool _showPageLoadingAnimation = true;
    private bool _extractAllText;
    private PdfColor _textSelectionColor = new(50f / 255f, 100f / 255f, 220f / 255f, 80f / 255f);
    private bool _searchMatchCase;
    private bool _searchMatchWholeWord;
    private bool _searchMatchDiacritics;
    private PdfColor _searchMatchColor = new(255f / 255f, 200f / 255f, 0f / 255f, 100f / 255f);
    private PdfColor _searchCurrentMatchColor = new(255f / 255f, 120f / 255f, 0f / 255f, 140f / 255f);
    private float _contentSelectionBorderThickness = 2f;
    private PdfColor _contentSelectionBorderColor = new(0f, 0f, 0f, 1f);
    private bool _allowTextSelection = true;
    private bool _allowPageContentHover = true;
    private bool _allowImageSelection = true;
    private bool _showSearchHighlights = true;
    private bool _usePageThumbnails = true;
    private bool _renderPageThumbnails = true;
    private PdfIntegerSize _thumbnailRenderMaximumSize = new(200, 200);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Configuration with the default settings.
    /// </summary>
    public static PdfPanelConfiguration Default { get; } = new();

    /// <summary>
    /// Minimum allowed zoom scale factor.
    /// </summary>
    public float MinScale
    {
        get => _minScale;
        set => SetProperty(ref _minScale, value, nameof(MinScale));
    }

    /// <summary>
    /// Maximum allowed zoom scale factor.
    /// </summary>
    public float MaxScale
    {
        get => _maxScale;
        set => SetProperty(ref _maxScale, value, nameof(MaxScale));
    }

    /// <summary>
    /// Proportional scale change of a single zoom step (e.g. 0.1 for 10%).
    /// </summary>
    public float ZoomStep
    {
        get => _zoomStep;
        set => SetProperty(ref _zoomStep, value, nameof(ZoomStep));
    }

    /// <summary>
    /// Gap between pages, in unscaled page space.
    /// </summary>
    public float PageGap
    {
        get => _pageGap;
        set => SetProperty(ref _pageGap, value, nameof(PageGap));
    }

    /// <summary>
    /// Padding from the edges of the viewing area to the pages, in device pixels.
    /// </summary>
    public PdfRectangle LayoutPadding
    {
        get => _layoutPadding;
        set => SetProperty(ref _layoutPadding, value, nameof(LayoutPadding));
    }

    /// <summary>
    /// Background color of the panel drawn behind the pages.
    /// </summary>
    public PdfColor PanelBackgroundColor
    {
        get => _panelBackgroundColor;
        set => SetProperty(ref _panelBackgroundColor, value, nameof(PanelBackgroundColor));
    }

    /// <summary>
    /// Corner radius of the pages, in unscaled page space.
    /// </summary>
    public float PageCornerRadius
    {
        get => _pageCornerRadius;
        set => SetProperty(ref _pageCornerRadius, value, nameof(PageCornerRadius));
    }

    /// <summary>
    /// Whether an animated placeholder is drawn over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation
    {
        get => _showPageLoadingAnimation;
        set => SetProperty(ref _showPageLoadingAnimation, value, nameof(ShowPageLoadingAnimation));
    }

    /// <summary>
    /// Whether the words of every page are extracted, not only of the pages that are rendered.
    /// </summary>
    public bool ExtractAllText
    {
        get => _extractAllText;
        set => SetProperty(ref _extractAllText, value, nameof(ExtractAllText));
    }

    /// <summary>
    /// Color the selected text is highlighted with.
    /// </summary>
    public PdfColor TextSelectionColor
    {
        get => _textSelectionColor;
        set => SetProperty(ref _textSelectionColor, value, nameof(TextSelectionColor));
    }

    /// <summary>
    /// Whether letter case must match in search.
    /// </summary>
    public bool SearchMatchCase
    {
        get => _searchMatchCase;
        set => SetProperty(ref _searchMatchCase, value, nameof(SearchMatchCase));
    }

    /// <summary>
    /// Whether a search match must start and end on word boundaries.
    /// </summary>
    public bool SearchMatchWholeWord
    {
        get => _searchMatchWholeWord;
        set => SetProperty(ref _searchMatchWholeWord, value, nameof(SearchMatchWholeWord));
    }

    /// <summary>
    /// Whether diacritics must match in search.
    /// </summary>
    public bool SearchMatchDiacritics
    {
        get => _searchMatchDiacritics;
        set => SetProperty(ref _searchMatchDiacritics, value, nameof(SearchMatchDiacritics));
    }

    /// <summary>
    /// Color the search matches are highlighted with.
    /// </summary>
    public PdfColor SearchMatchColor
    {
        get => _searchMatchColor;
        set => SetProperty(ref _searchMatchColor, value, nameof(SearchMatchColor));
    }

    /// <summary>
    /// Color the current search match is highlighted with.
    /// </summary>
    public PdfColor SearchCurrentMatchColor
    {
        get => _searchCurrentMatchColor;
        set => SetProperty(ref _searchCurrentMatchColor, value, nameof(SearchCurrentMatchColor));
    }

    /// <summary>
    /// Width of the border drawn around the selected content, in device pixels.
    /// </summary>
    public float ContentSelectionBorderThickness
    {
        get => _contentSelectionBorderThickness;
        set => SetProperty(ref _contentSelectionBorderThickness, value, nameof(ContentSelectionBorderThickness));
    }

    /// <summary>
    /// Color of the border drawn around the selected content.
    /// </summary>
    public PdfColor ContentSelectionBorderColor
    {
        get => _contentSelectionBorderColor;
        set => SetProperty(ref _contentSelectionBorderColor, value, nameof(ContentSelectionBorderColor));
    }

    /// <summary>
    /// Whether text can be selected with the pointer.
    /// </summary>
    public bool AllowTextSelection
    {
        get => _allowTextSelection;
        set => SetProperty(ref _allowTextSelection, value, nameof(AllowTextSelection));
    }

    /// <summary>
    /// Whether the page content under the pointer is tracked.
    /// </summary>
    public bool AllowPageContentHover
    {
        get => _allowPageContentHover;
        set => SetProperty(ref _allowPageContentHover, value, nameof(AllowPageContentHover));
    }

    /// <summary>
    /// Whether images can be selected with the pointer.
    /// </summary>
    public bool AllowImageSelection
    {
        get => _allowImageSelection;
        set => SetProperty(ref _allowImageSelection, value, nameof(AllowImageSelection));
    }

    /// <summary>
    /// Whether the search matches are highlighted.
    /// </summary>
    public bool ShowSearchHighlights
    {
        get => _showSearchHighlights;
        set => SetProperty(ref _showSearchHighlights, value, nameof(ShowSearchHighlights));
    }

    /// <summary>
    /// Whether the page's thumbnail image (/Thumb) is used as its thumbnail when present.
    /// </summary>
    public bool UsePageThumbnails
    {
        get => _usePageThumbnails;
        set => SetProperty(ref _usePageThumbnails, value, nameof(UsePageThumbnails));
    }

    /// <summary>
    /// Whether pages without a used thumbnail image are rendered as their thumbnail.
    /// </summary>
    public bool RenderPageThumbnails
    {
        get => _renderPageThumbnails;
        set => SetProperty(ref _renderPageThumbnails, value, nameof(RenderPageThumbnails));
    }

    /// <summary>
    /// Size in pixels a rendered page thumbnail is fitted within, keeping its aspect ratio.
    /// </summary>
    public PdfIntegerSize ThumbnailRenderMaximumSize
    {
        get => _thumbnailRenderMaximumSize;
        set => SetProperty(ref _thumbnailRenderMaximumSize, value, nameof(ThumbnailRenderMaximumSize));
    }

    /// <summary>
    /// Sets the settings on <paramref name="context"/>.
    /// </summary>
    public void ApplyConfiguration(PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.MinScale = MinScale;
        context.MaxScale = MaxScale;
        context.ZoomStep = ZoomStep;
        context.Layout.PageGap = PageGap;
        context.Layout.Padding = LayoutPadding;
        context.Renderer.BackgroundColor = PanelBackgroundColor;
        context.Renderer.PageCornerRadius = PageCornerRadius;
        context.Renderer.ShowPageLoadingAnimation = ShowPageLoadingAnimation;
        context.Text.ExtractText = ExtractAllText;
        context.Text.SelectionColor = TextSelectionColor;
        context.Text.AllowSelection = AllowTextSelection;
        context.Search.MatchCase = SearchMatchCase;
        context.Search.WholeWord = SearchMatchWholeWord;
        context.Search.MatchDiacritics = SearchMatchDiacritics;
        context.Search.MatchColor = SearchMatchColor;
        context.Search.CurrentMatchColor = SearchCurrentMatchColor;
        context.Search.ShowHighlights = ShowSearchHighlights;
        context.Content.SelectionBorderThickness = ContentSelectionBorderThickness;
        context.Content.SelectionBorderColor = ContentSelectionBorderColor;
        context.Content.AllowHover = AllowPageContentHover;
        context.Content.AllowImageSelection = AllowImageSelection;
        context.Thumbnails.UsePageThumbnails = UsePageThumbnails;
        context.Thumbnails.RenderPages = RenderPageThumbnails;
        context.Thumbnails.RenderMaximumSize = ThumbnailRenderMaximumSize;
    }

    private void SetProperty<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
