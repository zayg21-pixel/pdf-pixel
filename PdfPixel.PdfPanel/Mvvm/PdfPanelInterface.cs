using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Provides interface methods to control panel operations via MVVM pattern.
/// </summary>
public class PdfPanelInterface
{
    /// <summary>
    /// Raised when an action is requested; the panel host performs it.
    /// </summary>
    public event EventHandler<PdfPanelInterfaceActionEventArgs>? Requested;

    /// <summary>
    /// Increases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomIn() => Request(PdfPanelInterfaceAction.ZoomIn);

    /// <summary>
    /// Decreases the zoom level of the PDF panel by the configured scale factor.
    /// </summary>
    public void ZoomOut() => Request(PdfPanelInterfaceAction.ZoomOut);

    /// <summary>
    /// Selects the search result after the current one, wrapping to the first,
    /// or the first result on or after the current page when none is selected.
    /// </summary>
    public void NextSearchResult() => Request(PdfPanelInterfaceAction.NextSearchResult);

    /// <summary>
    /// Selects the search result before the current one, wrapping to the last,
    /// or the first result on or after the current page when none is selected.
    /// </summary>
    public void PreviousSearchResult() => Request(PdfPanelInterfaceAction.PreviousSearchResult);

    /// <summary>
    /// Requests a redraw of the PDF panel.
    /// </summary>
    public void RequestRedraw() => Request(PdfPanelInterfaceAction.RequestRedraw);

    /// <summary>
    /// Requests <see cref="OnAfterDraw"/> without full page rendering.
    /// </summary>
    public void RequestPresent() => Request(PdfPanelInterfaceAction.RequestPresent);

    /// <summary>
    /// Gets or sets the action invoked after each present with the canvas in panel pixels and the presented <see cref="PdfPanelFrame"/>.
    /// </summary>
    public Action<SKCanvas, PdfPanelFrame>? OnAfterDraw { get; set; }

    private void Request(PdfPanelInterfaceAction action) => Requested?.Invoke(this, new PdfPanelInterfaceActionEventArgs(action));
}
