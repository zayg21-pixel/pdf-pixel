using PdfPixel.Navigation;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Snap;
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
    /// Requests the panel to be drawn again from the content already produced, running <see cref="OnPanelPageRendered"/>
    /// and <see cref="OnPanelRendered"/> again.
    /// </summary>
    public void RequestPresent() => Request(PdfPanelInterfaceAction.RequestPresent);

    /// <summary>
    /// Scrolls the panel to a destination of the panel's document.
    /// </summary>
    /// <param name="destination">The destination to navigate to.</param>
    public void ScrollToDestination(PdfDestination destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        Requested?.Invoke(this, new PdfPanelInterfaceActionEventArgs(PdfPanelInterfaceAction.ScrollToDestination, destination));
    }

    /// <summary>
    /// Gets or sets the action that draws over each page every time it is drawn, as
    /// <see cref="PdfPanelRenderer.OnPanelPageRendered"/>.
    /// </summary>
    public Action<PdfSnapSurface, PdfPanelFramePage>? OnPanelPageRendered { get; set; }

    /// <summary>
    /// Gets or sets the action that draws over the panel every time a part of it is drawn, as
    /// <see cref="PdfPanelRenderer.OnPanelRendered"/>.
    /// </summary>
    public Action<PdfSnapSurface, PdfPanelFrame>? OnPanelRendered { get; set; }

    private void Request(PdfPanelInterfaceAction action) => Requested?.Invoke(this, new PdfPanelInterfaceActionEventArgs(action, null));
}
