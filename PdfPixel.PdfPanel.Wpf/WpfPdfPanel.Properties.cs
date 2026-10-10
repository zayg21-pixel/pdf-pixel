using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PdfPixel.PdfPanel.Wpf;

public partial class WpfPdfPanel
{
    /// <summary>
    /// Identifies the <see cref="Pages"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PagesProperty = DependencyProperty.Register(
        nameof(Pages),
        typeof(PdfPanelPageCollection),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, PagesProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="ScrollTick"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ScrollTickProperty = DependencyProperty.Register(
        nameof(ScrollTick),
        typeof(int),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="Scale"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale),
        typeof(double),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ScaleProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="Layout"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(IPdfPanelLayout),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="MinScale"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MinScaleProperty = DependencyProperty.Register(
        nameof(MinScale),
        typeof(double),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="MaxScale"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxScaleProperty = DependencyProperty.Register(
        nameof(MaxScale),
        typeof(double),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="ZoomStep"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register(
        nameof(ZoomStep),
        typeof(double),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="BackgroundColor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(
        nameof(BackgroundColor),
        typeof(System.Windows.Media.Color),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(System.Windows.Media.Color.FromRgb(211, 211, 211), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="PageCornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageCornerRadiusProperty = DependencyProperty.Register(
        nameof(PageCornerRadius),
        typeof(double),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="ShowPageLoadingAnimation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowPageLoadingAnimationProperty = DependencyProperty.Register(
        nameof(ShowPageLoadingAnimation),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SelectionColor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SelectionColorProperty = DependencyProperty.Register(
        nameof(SelectionColor),
        typeof(System.Windows.Media.Color),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(System.Windows.Media.Color.FromArgb(80, 50, 100, 220), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SearchMatchColor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchMatchColorProperty = DependencyProperty.Register(
        nameof(SearchMatchColor),
        typeof(System.Windows.Media.Color),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(System.Windows.Media.Color.FromArgb(100, 255, 200, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SearchCurrentMatchColor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchCurrentMatchColorProperty = DependencyProperty.Register(
        nameof(SearchCurrentMatchColor),
        typeof(System.Windows.Media.Color),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(System.Windows.Media.Color.FromArgb(140, 255, 120, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SearchMatchCase"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchMatchCaseProperty = DependencyProperty.Register(
        nameof(SearchMatchCase),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SearchWholeWord"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchWholeWordProperty = DependencyProperty.Register(
        nameof(SearchWholeWord),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="SearchMatchDiacritics"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchMatchDiacriticsProperty = DependencyProperty.Register(
        nameof(SearchMatchDiacritics),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="CurrentPage"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
        nameof(CurrentPage),
        typeof(int),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentPageProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="AutoScaleMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AutoScaleModeProperty = DependencyProperty.Register(
        nameof(AutoScaleMode),
        typeof(PdfPanelAutoScaleMode),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(
            PdfPanelAutoScaleMode.NoAutoScale,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            AutoScaleModeProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="AnnotationPopup"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AnnotationPopupProperty = DependencyProperty.Register(
        nameof(AnnotationPopup),
        typeof(PdfAnnotationPopup),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Identifies the <see cref="PanelInterface"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PanelInterfaceProperty = DependencyProperty.Register(
        nameof(PanelInterface),
        typeof(PdfPanelInterface),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, PanelInterfaceProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="SearchQuery"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchQueryProperty = DependencyProperty.Register(
        nameof(SearchQuery),
        typeof(string),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, SearchQueryProperty_Changed));

    private static readonly DependencyPropertyKey LayersPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Layers),
        typeof(PdfLayersViewModel),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="Layers"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LayersProperty = LayersPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey SearchResultsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SearchResults),
        typeof(ObservableCollection<PdfPanelSearchMatch>),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="SearchResults"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchResultsProperty = SearchResultsPropertyKey.DependencyProperty;

    /// <summary>
    /// Identifies the <see cref="CurrentSearchResult"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentSearchResultProperty = DependencyProperty.Register(
        nameof(CurrentSearchResult),
        typeof(PdfPanelSearchMatch?),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentSearchResultProperty_Changed));

    /// <summary>
    /// Identifies the <see cref="ExtractText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ExtractTextProperty = DependencyProperty.Register(
        nameof(ExtractText),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyPropertyKey IsTextExtractedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsTextExtracted),
        typeof(bool),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="IsTextExtracted"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTextExtractedProperty = IsTextExtractedPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey TextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Text),
        typeof(PdfPanelText),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="Text"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextProperty = TextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ActionsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Actions),
        typeof(PdfPanelActions),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="Actions"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActionsProperty = ActionsPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey PageLabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PageLabel),
        typeof(string),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="PageLabel"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageLabelProperty = PageLabelPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the page label string for the current page.
    /// </summary>
    public string PageLabel
    {
        get => (string)GetValue(PageLabelProperty);
    }

    /// <summary>
    /// Gets or sets the rendering backend for the panel.
    /// </summary>
    public WpfRenderMode RenderMode { get; set; } = WpfRenderMode.Software;

    /// <summary>
    /// Gets or sets the collection of pages.
    /// </summary>
    public PdfPanelPageCollection? Pages
    {
        get => (PdfPanelPageCollection?)GetValue(PagesProperty);
        set => SetValue(PagesProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll tick.
    /// New scroll position = Old scroll position + ScrollTick.
    /// </summary>
    public int ScrollTick
    {
        get => (int)GetValue(ScrollTickProperty);
        set => SetValue(ScrollTickProperty, value);
    }

    /// <summary>
    /// Gets or sets the scale.
    /// </summary>
    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the layout that positions the pages within the viewport.
    /// </summary>
    public IPdfPanelLayout Layout
    {
        get => (IPdfPanelLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum allowed zoom scale factor.
    /// </summary>
    public double MinScale
    {
        get => (double)GetValue(MinScaleProperty);
        set => SetValue(MinScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum allowed zoom scale factor.
    /// </summary>
    public double MaxScale
    {
        get => (double)GetValue(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the proportional scale change of a single zoom step (e.g. 0.1 for 10%).
    /// </summary>
    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    /// <summary>
    /// Gets or sets the background color drawn behind the pages.
    /// </summary>
    public System.Windows.Media.Color BackgroundColor
    {
        get => (System.Windows.Media.Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius for page rendering in unscaled page space.
    /// </summary>
    public double PageCornerRadius
    {
        get => (double)GetValue(PageCornerRadiusProperty);
        set => SetValue(PageCornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets whether an animated placeholder is drawn over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation
    {
        get => (bool)GetValue(ShowPageLoadingAnimationProperty);
        set => SetValue(ShowPageLoadingAnimationProperty, value);
    }

    /// <summary>
    /// Gets or sets the color the selected text is highlighted with.
    /// </summary>
    public System.Windows.Media.Color SelectionColor
    {
        get => (System.Windows.Media.Color)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color the search matches are highlighted with.
    /// </summary>
    public System.Windows.Media.Color SearchMatchColor
    {
        get => (System.Windows.Media.Color)GetValue(SearchMatchColorProperty);
        set => SetValue(SearchMatchColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the color the current search match is highlighted with.
    /// </summary>
    public System.Windows.Media.Color SearchCurrentMatchColor
    {
        get => (System.Windows.Media.Color)GetValue(SearchCurrentMatchColorProperty);
        set => SetValue(SearchCurrentMatchColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether letter case must match.
    /// </summary>
    public bool SearchMatchCase
    {
        get => (bool)GetValue(SearchMatchCaseProperty);
        set => SetValue(SearchMatchCaseProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a match must start and end on word boundaries.
    /// </summary>
    public bool SearchWholeWord
    {
        get => (bool)GetValue(SearchWholeWordProperty);
        set => SetValue(SearchWholeWordProperty, value);
    }

    /// <summary>
    /// Gets or sets whether diacritics must match.
    /// </summary>
    public bool SearchMatchDiacritics
    {
        get => (bool)GetValue(SearchMatchDiacriticsProperty);
        set => SetValue(SearchMatchDiacriticsProperty, value);
    }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    /// <summary>
    /// Gets or sets the automatic scaling mode.
    /// </summary>
    public PdfPanelAutoScaleMode AutoScaleMode
    {
        get => (PdfPanelAutoScaleMode)GetValue(AutoScaleModeProperty);
        set => SetValue(AutoScaleModeProperty, value);
    }

    /// <summary>
    /// Annotation popup under the mouse cursor.
    /// </summary>
    public PdfAnnotationPopup? AnnotationPopup
    {
        get => (PdfAnnotationPopup?)GetValue(AnnotationPopupProperty);
        set => SetValue(AnnotationPopupProperty, value);
    }

    /// <summary>
    /// Annotation tooltip template.
    /// </summary>
    public ToolTip? AnnotationToolTip { get; set; }

    /// <summary>
    /// Gets or sets the text to search for in the document, or <see langword="null"/> when no search is active.
    /// </summary>
    public string? SearchQuery
    {
        get => (string?)GetValue(SearchQueryProperty);
        set => SetValue(SearchQueryProperty, value);
    }

    /// <summary>
    /// Gets the layers (optional content) of the shown document, or null when no pages are shown.
    /// </summary>
    public PdfLayersViewModel? Layers
    {
        get => (PdfLayersViewModel?)GetValue(LayersProperty);
    }

    /// <summary>
    /// Gets the results of <see cref="SearchQuery"/> found so far, ordered by page.
    /// </summary>
    public ObservableCollection<PdfPanelSearchMatch> SearchResults
    {
        get => (ObservableCollection<PdfPanelSearchMatch>)GetValue(SearchResultsProperty);
    }

    /// <summary>
    /// Gets or sets whether the words of every page are extracted, not only of the pages that are rendered.
    /// </summary>
    public bool ExtractText
    {
        get => (bool)GetValue(ExtractTextProperty);
        set => SetValue(ExtractTextProperty, value);
    }

    /// <summary>
    /// Gets whether the words of every page of the current document have been extracted.
    /// </summary>
    public bool IsTextExtracted
    {
        get => (bool)GetValue(IsTextExtractedProperty);
    }

    /// <summary>
    /// Gets the text of the current document, or <see langword="null"/> when no document is shown.
    /// </summary>
    public PdfPanelText? Text
    {
        get => (PdfPanelText?)GetValue(TextProperty);
    }

    /// <summary>
    /// Gets the actions of the current document, or <see langword="null"/> when no document is shown.
    /// </summary>
    public PdfPanelActions? Actions
    {
        get => (PdfPanelActions?)GetValue(ActionsProperty);
    }

    /// <summary>
    /// Gets or sets the search result the panel navigates to.
    /// </summary>
    public PdfPanelSearchMatch? CurrentSearchResult
    {
        get => (PdfPanelSearchMatch?)GetValue(CurrentSearchResultProperty);
        set => SetValue(CurrentSearchResultProperty, value);
    }

    /// <summary>
    /// Gets or sets the panel interface for controlling panel operations via MVVM.
    /// </summary>
    public PdfPanelInterface? PanelInterface
    {
        get => (PdfPanelInterface?)GetValue(PanelInterfaceProperty);
        set => SetValue(PanelInterfaceProperty, value);
    }

    private static void PagesProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (source.Pages == null)
        {
            source.ResetContent();
        }
        else
        {
            if (source.CurrentPage > 0 && source.CurrentPage <= source.Pages.Count)
            {
                PdfPanelPage page = source.Pages[source.CurrentPage - 1];
                string label = page.Info.Label;
                source.SetValue(PageLabelPropertyKey, label);
            }
        }
    }

    private static void ScaleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (!source._updatingScale)
        {
            source.OnScaleChanged();
        }
    }

    private static void AutoScaleModeProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (!source._updatingScale && source._context != null)
        {
            source._context.AutoScaleMode = source.AutoScaleMode;
        }
    }

    private static void CurrentPageProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (source.Pages == null)
        {
            source.ResetContent();
            source.SetValue(PageLabelPropertyKey, string.Empty);
            return;
        }

        if (source.Pages.Count == 0)
        {
            source.SetValue(PageLabelPropertyKey, string.Empty);
            return;
        }

        if (source.CurrentPage < 1)
        {
            source.CurrentPage = 1;
        }

        if (source.CurrentPage > source.Pages.Count)
        {
            source.CurrentPage = source.Pages.Count;
        }

        PdfPanelPage page = source.Pages[source.CurrentPage - 1];
        string label = page.Info.Label;
        source.SetValue(PageLabelPropertyKey, label);

        if (!source._updatingPages)
        {
            source.ScrollToPage(source.CurrentPage);
        }
    }

    private static void SearchQueryProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        source.InvalidateVisual();
    }

    private static void CurrentSearchResultProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (e.NewValue is PdfPanelSearchMatch searchMatch && source._context != null)
        {
            source._context.ScrollToSearchMatch(searchMatch);
        }

        source.InvalidateVisual();
    }

    private static void PanelInterfaceProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (e.OldValue is PdfPanelInterface oldInterface)
        {
            oldInterface.Requested -= source.HandleInterfaceRequest;
        }

        if (e.NewValue is PdfPanelInterface newInterface)
        {
            newInterface.Requested += source.HandleInterfaceRequest;
        }
    }
}
