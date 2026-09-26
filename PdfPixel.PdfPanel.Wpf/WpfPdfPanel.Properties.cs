using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Settings;
using PdfPixel.PdfPanel.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PdfPixel.PdfPanel.Wpf;

public partial class WpfPdfPanel
{
    public static readonly DependencyProperty PagesProperty = DependencyProperty.Register(nameof(Pages), typeof(PdfPanelPageCollection), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, PagesProperty_Changed));

    public static readonly DependencyProperty ScrollTickProperty = DependencyProperty.Register(nameof(ScrollTick), typeof(int), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ScaleProperty_Changed));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(nameof(Appearance), typeof(PdfPanelAppearanceSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(PdfPanelZoomSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(IPdfPanelLayout), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(nameof(Interaction), typeof(PdfPanelInteractionSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty SearchProperty = DependencyProperty.Register(nameof(Search), typeof(PdfPanelSearchSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(PdfPanelTextSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty RenderingProperty = DependencyProperty.Register(nameof(Rendering), typeof(PdfPanelRenderingSettings), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, SettingsGroupProperty_Changed));

    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentPageProperty_Changed));

    public static readonly DependencyProperty AutoScaleModeProperty = DependencyProperty.Register(nameof(AutoScaleMode), typeof(PdfPanelAutoScaleMode), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(PdfPanelAutoScaleMode.NoAutoScale, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty AnnotationPopupProperty = DependencyProperty.Register(nameof(AnnotationPopup), typeof(PdfAnnotationPopup), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PanelInterfaceProperty = DependencyProperty.Register(nameof(PanelInterface), typeof(WpfPdfPanelInterface), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, PanelInterfaceProperty_Changed));

    public static readonly DependencyProperty SearchQueryProperty = DependencyProperty.Register(nameof(SearchQuery), typeof(string), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, SearchQueryProperty_Changed));

    public static readonly DependencyPropertyKey SearchResultsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(SearchResults), typeof(ObservableCollection<PdfPanelSearchMatch>), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));
    public static readonly DependencyProperty SearchResultsProperty = SearchResultsPropertyKey.DependencyProperty;

    public static readonly DependencyProperty CurrentSearchResultProperty = DependencyProperty.Register(nameof(CurrentSearchResult), typeof(PdfPanelSearchMatch?), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentSearchResultProperty_Changed));

    public static readonly DependencyPropertyKey TextLayerPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(TextLayer), typeof(PdfPanelTextLayer), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None));
    public static readonly DependencyProperty TextLayerProperty = TextLayerPropertyKey.DependencyProperty;

    public static readonly DependencyPropertyKey PageLabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PageLabel), typeof(string), typeof(WpfPdfPanel),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.None));
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
    public PdfPanelPageCollection Pages
    {
        get => (PdfPanelPageCollection)GetValue(PagesProperty);
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
    /// Gets or sets the colors and decorations of the panel.
    /// </summary>
    public PdfPanelAppearanceSettings Appearance
    {
        get => (PdfPanelAppearanceSettings)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the scale limits and zoom step.
    /// </summary>
    public PdfPanelZoomSettings Zoom
    {
        get => (PdfPanelZoomSettings)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
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
    /// Gets or sets the pointer interaction thresholds.
    /// </summary>
    public PdfPanelInteractionSettings Interaction
    {
        get => (PdfPanelInteractionSettings)GetValue(InteractionProperty);
        set => SetValue(InteractionProperty, value);
    }

    /// <summary>
    /// Gets or sets the options that control how <see cref="SearchQuery"/> is matched.
    /// </summary>
    public PdfPanelSearchSettings Search
    {
        get => (PdfPanelSearchSettings)GetValue(SearchProperty);
        set => SetValue(SearchProperty, value);
    }

    /// <summary>
    /// Gets or sets the text extraction and text highlight options.
    /// </summary>
    public PdfPanelTextSettings Text
    {
        get => (PdfPanelTextSettings)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the rendering quality and timing.
    /// </summary>
    public PdfPanelRenderingSettings Rendering
    {
        get => (PdfPanelRenderingSettings)GetValue(RenderingProperty);
        set => SetValue(RenderingProperty, value);
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
    public PdfAnnotationPopup AnnotationPopup
    {
        get => (PdfAnnotationPopup)GetValue(AnnotationPopupProperty);
        set => SetValue(AnnotationPopupProperty, value);
    }

    /// <summary>
    /// Annotation tooltip template.
    /// </summary>
    public ToolTip AnnotationToolTip { get; set; }

    /// <summary>
    /// Gets or sets the text to search for in the document, or <see langword="null"/> when no search is active.
    /// </summary>
    public string SearchQuery
    {
        get => (string)GetValue(SearchQueryProperty);
        set => SetValue(SearchQueryProperty, value);
    }

    /// <summary>
    /// Gets the results of <see cref="SearchQuery"/> found so far, ordered by page.
    /// </summary>
    public ObservableCollection<PdfPanelSearchMatch> SearchResults
    {
        get => (ObservableCollection<PdfPanelSearchMatch>)GetValue(SearchResultsProperty);
    }

    /// <summary>
    /// Gets the text layer of the current document, or <see langword="null"/> when no document is shown.
    /// </summary>
    public PdfPanelTextLayer TextLayer
    {
        get => (PdfPanelTextLayer)GetValue(TextLayerProperty);
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
    public WpfPdfPanelInterface PanelInterface
    {
        get => (WpfPdfPanelInterface)GetValue(PanelInterfaceProperty);
        set => SetValue(PanelInterfaceProperty, value);
    }

    private static void PagesProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;

        if (e.NewValue is null)
        {
            source.ResetContent();
        }
        else
        {
            if (source.CurrentPage > 0 && source.CurrentPage <= source.Pages.Count)
            {
                var page = source.Pages[source.CurrentPage - 1];
                var label = page.Info.Label;
                source.SetValue(PageLabelPropertyKey, label);
            }
        }
    }

    private static void ScaleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;
        PdfPanelZoomSettings zoom = source._settings.Zoom;

        if (source.Scale < zoom.MinScale)
        {
            source.Scale = zoom.MinScale;
        }

        if (source.Scale > zoom.MaxScale)
        {
            source.Scale = zoom.MaxScale;
        }

        if (!source._updatingScale)
        {
            source.AutoScaleMode = PdfPanelAutoScaleMode.NoAutoScale;
            source.OnScaleChanged();
        }
    }

    private static void SettingsGroupProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var source = (WpfPdfPanel)d;
        PdfPanelSettings settings = source._settings;

        if (e.Property == AppearanceProperty)
        {
            settings.Appearance = (PdfPanelAppearanceSettings)e.NewValue;
        }
        else if (e.Property == ZoomProperty)
        {
            settings.Zoom = (PdfPanelZoomSettings)e.NewValue;
        }
        else if (e.Property == LayoutProperty)
        {
            settings.Layout = (IPdfPanelLayout)e.NewValue;
        }
        else if (e.Property == InteractionProperty)
        {
            settings.Interaction = (PdfPanelInteractionSettings)e.NewValue;
        }
        else if (e.Property == SearchProperty)
        {
            settings.Search = (PdfPanelSearchSettings)e.NewValue;
        }
        else if (e.Property == TextProperty)
        {
            settings.Text = (PdfPanelTextSettings)e.NewValue;
        }
        else if (e.Property == RenderingProperty)
        {
            settings.Rendering = (PdfPanelRenderingSettings)e.NewValue;
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

        var page = source.Pages[source.CurrentPage - 1];
        var label = page.Info.Label;
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

        if (e.OldValue is WpfPdfPanelInterface oldInterface)
        {
            oldInterface.OnRequest = null;
        }

        if (e.NewValue is WpfPdfPanelInterface newInterface)
        {
            newInterface.OnRequest = source.HandleInterfaceRequest;
        }
    }
}