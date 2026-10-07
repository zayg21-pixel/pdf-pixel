using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Provides interface methods to control WpfPdfPanel operations via MVVM pattern.
/// </summary>
public class WpfPdfPanelInterface
{
    /// <summary>
    /// Internal delegate invoked when an action is requested.
    /// </summary>
    internal Action<PdfPanelInterfaceAction>? OnRequest { get; set; }

    /// <summary>
    /// Increases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomIn() => OnRequest?.Invoke(PdfPanelInterfaceAction.ZoomIn);

    /// <summary>
    /// Decreases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomOut() => OnRequest?.Invoke(PdfPanelInterfaceAction.ZoomOut);

    /// <summary>
    /// Selects the search result after the current one, wrapping to the first,
    /// or the first result on or after the current page when none is selected.
    /// </summary>
    public void NextSearchResult() => OnRequest?.Invoke(PdfPanelInterfaceAction.NextSearchResult);

    /// <summary>
    /// Selects the search result before the current one, wrapping to the last,
    /// or the first result on or after the current page when none is selected.
    /// </summary>
    public void PreviousSearchResult() => OnRequest?.Invoke(PdfPanelInterfaceAction.PreviousSearchResult);

    /// <summary>
    /// Requests a redraw of the PDF panel.
    /// </summary>
    public void RequestRedraw() => OnRequest?.Invoke(PdfPanelInterfaceAction.RequestRedraw);

    /// <summary>
    /// Requests <see cref="OnAfterDraw"/> without full page rendering.
    /// </summary>
    public void RequestPresent() => OnRequest?.Invoke(PdfPanelInterfaceAction.RequestPresent);

    /// <summary>
    /// Gets or sets the action invoked after each present with the canvas in panel pixels and the presented <see cref="PdfPanelFrame"/>.
    /// </summary>
    public Action<SKCanvas, PdfPanelFrame>? OnAfterDraw { get; set; }
}
