using Microsoft.Maui.Controls;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Mvvm;

namespace PdfPixel.PdfPanel.Maui;

public partial class MauiPdfPanel
{
    /// <summary>
    /// Identifies the <see cref="Document"/> bindable property.
    /// </summary>
    public static readonly BindableProperty DocumentProperty = BindableProperty.Create(
        nameof(Document),
        typeof(PdfPanelDocument),
        typeof(MauiPdfPanel),
        null,
        propertyChanged: DocumentProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="ZoomScale"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ZoomScaleProperty = BindableProperty.Create(
        nameof(ZoomScale),
        typeof(double),
        typeof(MauiPdfPanel),
        1d,
        BindingMode.TwoWay,
        propertyChanged: ZoomScaleProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="PageLayout"/> bindable property.
    /// </summary>
    public static readonly BindableProperty PageLayoutProperty = BindableProperty.Create(
        nameof(PageLayout),
        typeof(IPdfPanelLayout),
        typeof(MauiPdfPanel),
        null,
        propertyChanged: RedrawProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="Configuration"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ConfigurationProperty = BindableProperty.Create(
        nameof(Configuration),
        typeof(PdfPanelConfiguration),
        typeof(MauiPdfPanel),
        null,
        propertyChanged: ConfigurationProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="CurrentPage"/> bindable property.
    /// </summary>
    public static readonly BindableProperty CurrentPageProperty = BindableProperty.Create(
        nameof(CurrentPage),
        typeof(int),
        typeof(MauiPdfPanel),
        1,
        BindingMode.TwoWay,
        propertyChanged: CurrentPageProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="AutoScaleMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty AutoScaleModeProperty = BindableProperty.Create(
        nameof(AutoScaleMode),
        typeof(PdfPanelAutoScaleMode),
        typeof(MauiPdfPanel),
        PdfPanelAutoScaleMode.NoAutoScale,
        BindingMode.TwoWay,
        propertyChanged: AutoScaleModeProperty_Changed);

    /// <summary>
    /// Identifies the <see cref="PanelInterface"/> bindable property.
    /// </summary>
    public static readonly BindableProperty PanelInterfaceProperty = BindableProperty.Create(
        nameof(PanelInterface),
        typeof(PdfPanelInterface),
        typeof(MauiPdfPanel),
        null,
        propertyChanged: PanelInterfaceProperty_Changed);

    private static readonly BindablePropertyKey PageLabelPropertyKey = BindableProperty.CreateReadOnly(
        nameof(PageLabel),
        typeof(string),
        typeof(MauiPdfPanel),
        string.Empty);

    /// <summary>
    /// Identifies the <see cref="PageLabel"/> bindable property.
    /// </summary>
    public static readonly BindableProperty PageLabelProperty = PageLabelPropertyKey.BindableProperty;

    /// <summary>
    /// Gets the page label string for the current page.
    /// </summary>
    public string PageLabel
    {
        get => (string)GetValue(PageLabelProperty);
    }

    /// <summary>
    /// Gets or sets the displayed document.
    /// </summary>
    public PdfPanelDocument? Document
    {
        get => (PdfPanelDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>
    /// Gets or sets the zoom factor of the pages.
    /// </summary>
    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the layout that positions the pages within the viewport.
    /// </summary>
    public IPdfPanelLayout PageLayout
    {
        get => (IPdfPanelLayout)GetValue(PageLayoutProperty);
        set => SetValue(PageLayoutProperty, value);
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
    /// Gets or sets the panel interface for controlling panel operations via MVVM.
    /// </summary>
    public PdfPanelInterface? PanelInterface
    {
        get => (PdfPanelInterface?)GetValue(PanelInterfaceProperty);
        set => SetValue(PanelInterfaceProperty, value);
    }

    private static void DocumentProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (source.Document == null)
        {
            source.ResetContent();
        }
        else
        {
            if (source.CurrentPage > 0 && source.CurrentPage <= source.Document.Pages.Count)
            {
                PdfPanelPage page = source.Document.Pages[source.CurrentPage - 1];
                string label = page.Label;
                source.SetValue(PageLabelPropertyKey, label);
            }
        }

        source.RequestRedraw();
    }

    private static void ZoomScaleProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (source._updatingScale || source._context == null)
        {
            return;
        }

        source._context.Zoom((float)source.ZoomScale * source._context.ReferenceScale, source._context.PanelWidth / 2, source._context.PanelHeight / 2);
        source.RequestRedraw();
    }

    private static void RedrawProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
        => ((MauiPdfPanel)bindable).RequestRedraw();

    private static void AutoScaleModeProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (!source._updatingScale && source._context != null)
        {
            source._context.AutoScaleMode = source.AutoScaleMode;
            source.RequestRedraw();
        }
    }

    private static void CurrentPageProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (source.Document == null)
        {
            source.ResetContent();
            source.SetValue(PageLabelPropertyKey, string.Empty);
            return;
        }

        if (source.Document.Pages.Count == 0)
        {
            source.SetValue(PageLabelPropertyKey, string.Empty);
            return;
        }

        if (source.CurrentPage < 1)
        {
            source.CurrentPage = 1;
        }

        if (source.CurrentPage > source.Document.Pages.Count)
        {
            source.CurrentPage = source.Document.Pages.Count;
        }

        PdfPanelPage page = source.Document.Pages[source.CurrentPage - 1];
        string label = page.Label;
        source.SetValue(PageLabelPropertyKey, label);

        if (!source._updatingPages)
        {
            source.ScrollToPage(source.CurrentPage);
        }
    }

    private static void PanelInterfaceProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (oldValue is PdfPanelInterface oldInterface)
        {
            oldInterface.Requested -= source.HandleInterfaceRequest;
        }

        if (newValue is PdfPanelInterface newInterface)
        {
            newInterface.Requested += source.HandleInterfaceRequest;
        }
    }

    private static void ConfigurationProperty_Changed(BindableObject bindable, object? oldValue, object? newValue)
    {
        var source = (MauiPdfPanel)bindable;

        if (oldValue is PdfPanelConfiguration oldConfiguration)
        {
            oldConfiguration.PropertyChanged -= source.OnConfigurationChanged;
        }

        if (newValue is PdfPanelConfiguration newConfiguration)
        {
            newConfiguration.PropertyChanged += source.OnConfigurationChanged;
        }

        source.RequestRedraw();
    }
}
