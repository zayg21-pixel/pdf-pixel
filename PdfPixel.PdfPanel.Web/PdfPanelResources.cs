using PdfPixel.Actions.Model;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates all resources associated with a single PDF panel instance.
/// </summary>
[SupportedOSPlatform("browser")]
internal class PdfPanelResources
{
    /// <summary>
    /// Initializes the resources of the panel in the container with the given id.
    /// </summary>
    public PdfPanelResources(string containerId) => ContainerId = containerId;

    /// <summary>
    /// Gets the id of the container the panel is registered for.
    /// </summary>
    public string ContainerId { get; }

    /// <summary>
    /// Gets or sets the render target factory for the panel.
    /// </summary>
    public IPdfPanelRenderTargetFactory RenderTargetFactory { get; set; }

    /// <summary>
    /// Gets or sets the Skia surface factory for the panel.
    /// </summary>
    public ISkSurfaceFactory SkSurfaceFactory { get; set; }

    /// <summary>
    /// Gets or sets the PDF panel context instance.
    /// </summary>
    public PdfPanelContext Context { get; set; }

    /// <summary>
    /// Gets or sets the document currently loaded into the panel.
    /// </summary>
    public IPdfDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the pages of the currently loaded document.
    /// </summary>
    public PdfPanelPageCollection Pages { get; set; }

    /// <summary>
    /// Gets the panel values set through the panel configuration.
    /// </summary>
    public PdfPanelConfiguration Configuration { get; } = new();

    /// <summary>
    /// Gets or sets the minimum time between two yields of page decoding.
    /// </summary>
    public TimeSpan YieldInterval { get; set; }

    /// <summary>
    /// Gets or sets the annotation popup last sent to JS, or null when none is shown.
    /// </summary>
    public PdfAnnotationPopup AnnotationPopup { get; set; }

    /// <summary>
    /// Gets or sets whether the search matches changed since they were last sent to JS.
    /// </summary>
    public bool SearchResultsChanged { get; set; }

    /// <summary>
    /// Gets or sets the range of the current search result last received from JS, or null when there is none.
    /// </summary>
    public PdfPanelTextRange? CurrentSearchRange { get; set; }

    /// <summary>
    /// Gets or sets the URI requested during the last synchronization for JS to open, or empty when none was requested.
    /// </summary>
    public string OpenUri { get; set; } = string.Empty;

    /// <summary>
    /// Keeps the first URI requested during a synchronization for JS to open.
    /// </summary>
    public void OnUriRequested(object sender, PdfPanelActionEventArgs<PdfUriAction> args)
    {
        if (args.Action.Uri != null && OpenUri.Length == 0)
        {
            OpenUri = args.Action.Uri.Value.ToString();
        }
    }

    /// <summary>
    /// Handles a request to open the document of a remote go-to action.
    /// </summary>
    public void OnRemoteDocumentRequested(object sender, PdfPanelActionEventArgs<PdfGoToRemoteAction> args)
    {
        // TODO: handle remote file loading
    }

    /// <summary>
    /// Marks the search matches as changed and schedules a redraw that sends them to JS.
    /// </summary>
    public void OnSearchMatchesChanged(object sender, EventArgs args)
    {
        SearchResultsChanged = true;
        PdfPanelInterop.ScheduleRedraw(ContainerId);
    }

    /// <summary>
    /// Schedules a redraw that sends the extracted text state to JS.
    /// </summary>
    public void OnTextExtracted(object sender, EventArgs args) => PdfPanelInterop.ScheduleRedraw(ContainerId);
}