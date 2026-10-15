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
    /// Identifies the <see cref="Configuration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ConfigurationProperty = DependencyProperty.Register(
        nameof(Configuration),
        typeof(PdfPanelConfiguration),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, ConfigurationProperty_Changed));

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

    private static readonly DependencyPropertyKey OutlinesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Outlines),
        typeof(PdfOutlinesViewModel),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="Outlines"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OutlinesProperty = OutlinesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ThumbnailsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Thumbnails),
        typeof(PdfThumbnailsViewModel),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="Thumbnails"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ThumbnailsProperty = ThumbnailsPropertyKey.DependencyProperty;

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

    private static readonly DependencyPropertyKey ActiveContentNamePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ActiveContentName),
        typeof(string),
        typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));

    /// <summary>
    /// Identifies the <see cref="ActiveContentName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActiveContentNameProperty = ActiveContentNamePropertyKey.DependencyProperty;

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
    /// Gets or sets the settings of the panel.
    /// </summary>
    public PdfPanelConfiguration? Configuration
    {
        get => (PdfPanelConfiguration?)GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    /// <summary>
    /// Gets the alternate description (/Alt) of the content under the mouse cursor, or <see langword="null"/> if there is none.
    /// </summary>
    public string? ActiveContentName
    {
        get => (string?)GetValue(ActiveContentNameProperty);
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
    /// Gets the outline (bookmarks) of the shown document, or null when no pages are shown.
    /// </summary>
    public PdfOutlinesViewModel? Outlines
    {
        get => (PdfOutlinesViewModel?)GetValue(OutlinesProperty);
    }

    /// <summary>
    /// Gets the page thumbnails of the shown document, or null when no pages are shown.
    /// </summary>
    public PdfThumbnailsViewModel? Thumbnails
    {
        get => (PdfThumbnailsViewModel?)GetValue(ThumbnailsProperty);
    }

    /// <summary>
    /// Gets the results of <see cref="SearchQuery"/> found so far, ordered by page.
    /// </summary>
    public ObservableCollection<PdfPanelSearchMatch> SearchResults
    {
        get => (ObservableCollection<PdfPanelSearchMatch>)GetValue(SearchResultsProperty);
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

    private static void ConfigurationProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (e.OldValue is PdfPanelConfiguration oldConfiguration)
        {
            oldConfiguration.PropertyChanged -= source.OnViewModelChanged;
        }

        if (e.NewValue is PdfPanelConfiguration newConfiguration)
        {
            newConfiguration.PropertyChanged += source.OnViewModelChanged;
        }
    }
}
