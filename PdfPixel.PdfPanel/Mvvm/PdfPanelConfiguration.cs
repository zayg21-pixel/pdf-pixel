using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Settings of a panel, applied to a <see cref="PdfPanelContext"/> with <see cref="ApplyConfiguration"/>.
/// </summary>
public sealed class PdfPanelConfiguration : INotifyPropertyChanged
{
    private float _minScale = PdfPanelContext.DefaultMinScale;
    private float _maxScale = PdfPanelContext.DefaultMaxScale;
    private float _zoomStep = PdfPanelContext.DefaultZoomStep;
    private float _pageGap = PdfPanelVerticalLayout.DefaultPageGap;
    private PdfRectangle _layoutPadding = PdfPanelVerticalLayout.DefaultPadding;
    private PdfColor _panelBackgroundColor = PdfPanelRenderer.DefaultBackgroundColor;
    private float _pageCornerRadius = PdfPanelRenderer.DefaultPageCornerRadius;
    private bool _showPageLoadingAnimation = PdfPanelRenderer.DefaultShowPageLoadingAnimation;
    private bool _extractAllText = PdfPanelText.DefaultExtractText;
    private PdfColor _textSelectionColor = PdfPanelText.DefaultSelectionColor;
    private bool _searchMatchCase = PdfPanelSearch.DefaultMatchCase;
    private bool _searchMatchWholeWord = PdfPanelSearch.DefaultWholeWord;
    private bool _searchMatchDiacritics = PdfPanelSearch.DefaultMatchDiacritics;
    private PdfColor _searchMatchColor = PdfPanelSearch.DefaultMatchColor;
    private PdfColor _searchCurrentMatchColor = PdfPanelSearch.DefaultCurrentMatchColor;
    private float _contentSelectionBorderThickness = PdfPanelContent.DefaultSelectionBorderThickness;
    private PdfColor _contentSelectionBorderColor = PdfPanelContent.DefaultSelectionBorderColor;
    private bool _allowTextSelection = PdfPanelText.DefaultAllowSelection;
    private bool _allowPageContentHover = PdfPanelContent.DefaultAllowHover;
    private bool _allowImageSelection = PdfPanelContent.DefaultAllowImageSelection;
    private bool _showSearchHighlights = PdfPanelSearch.DefaultShowHighlights;
    private bool _usePageThumbnails = PdfPanelThumbnails.DefaultUsePageThumbnails;
    private bool _renderPageThumbnails = PdfPanelThumbnails.DefaultRenderPages;
    private PdfIntegerSize _thumbnailRenderMaximumSize = PdfPanelThumbnails.DefaultRenderMaximumSize;

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
    /// Gap between pages, in unscaled panel space.
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
    /// Corner radius of the pages, in unscaled panel space.
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
    /// Whether the page's embedded thumbnail image is used as its thumbnail when present.
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
