using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Defines actions that can be performed on the PDF panel.
/// </summary>
internal enum PdfPanelInterfaceAction
{
    /// <summary>
    /// Increase zoom level.
    /// </summary>
    ZoomIn,

    /// <summary>
    /// Decrease zoom level.
    /// </summary>
    ZoomOut,

    /// <summary>
    /// Request panel redraw.
    /// </summary>
    RequestRedraw,

    /// <summary>
    /// Requests <see cref="WpfPdfPanelInterface.OnAfterDraw"/> without full page rendering.
    /// </summary>
    RequestRefresh
}

/// <summary>
/// Provides interface methods to control WpfPdfPanel operations via MVVM pattern.
/// </summary>
public class WpfPdfPanelInterface
{
    /// <summary>
    /// Internal delegate invoked when an action is requested.
    /// </summary>
    internal Action<PdfPanelInterfaceAction> OnRequest { get; set; }

    /// <summary>
    /// Increases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomIn()
    {
        OnRequest?.Invoke(PdfPanelInterfaceAction.ZoomIn);
    }

    /// <summary>
    /// Decreases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomOut()
    {
        OnRequest?.Invoke(PdfPanelInterfaceAction.ZoomOut);
    }

    /// <summary>
    /// Requests a redraw of the PDF panel.
    /// </summary>
    public void RequestRedraw()
    {
        OnRequest?.Invoke(PdfPanelInterfaceAction.RequestRedraw);
    }

    /// <summary>
    /// Requests <see cref="OnAfterDraw"/> without full page rendering.
    /// </summary>
    public void RequestRefresh()
    {
        OnRequest?.Invoke(PdfPanelInterfaceAction.RequestRefresh);
    }

    /// <summary>
    /// Gets or sets the action to execute after the drawing operation is completed.
    /// </summary>
    /// <remarks>The specified action receives the current <see cref="SKCanvas"/> and the associated <see
    /// cref="DrawingRequest"/> as parameters, allowing for custom post-processing or additional drawing steps.
    /// Assigning this property enables users to inject custom logic immediately following the main drawing
    /// routine.</remarks>
    public Action<SKCanvas, DrawingRequest> OnAfterDraw { get; set; }
}
