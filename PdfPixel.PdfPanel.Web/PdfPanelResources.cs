using PdfPixel.Encryption;
using PdfPixel.Files;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates all resources associated with a single PDF panel instance.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class PdfPanelResources
{
    /// <summary>
    /// Initializes the resources of the panel in the container with the given id.
    /// </summary>
    public PdfPanelResources(string containerId, ISkSurfaceFactory skSurfaceFactory, IPdfPanelRenderTargetFactory renderTargetFactory)
    {
        ContainerId = containerId;
        SkSurfaceFactory = skSurfaceFactory;
        RenderTargetFactory = renderTargetFactory;
    }

    /// <summary>
    /// Gets the id of the container the panel is registered for.
    /// </summary>
    public string ContainerId { get; }

    /// <summary>
    /// Gets the render target factory for the panel.
    /// </summary>
    public IPdfPanelRenderTargetFactory RenderTargetFactory { get; }

    /// <summary>
    /// Gets the Skia surface factory for the panel.
    /// </summary>
    public ISkSurfaceFactory SkSurfaceFactory { get; }

    /// <summary>
    /// Gets or sets the PDF panel context instance, or null when no document is loaded.
    /// </summary>
    public PdfPanelContext? Context { get; set; }

    /// <summary>
    /// Gets or sets the document currently loaded into the panel, or null when none is loaded.
    /// </summary>
    public PdfPanelDocument? Document { get; set; }

    /// <summary>
    /// Gets or sets the outline of the currently loaded document, or null when none is loaded.
    /// </summary>
    public PdfOutlinesViewModel? Outlines { get; set; }

    /// <summary>
    /// Gets or sets the layers of the currently loaded document, or null when none is loaded.
    /// </summary>
    public PdfLayersViewModel? Layers { get; set; }

    /// <summary>
    /// Gets or sets the page thumbnails of the currently loaded document, or null when none is loaded.
    /// </summary>
    public PdfThumbnailsViewModel? Thumbnails { get; set; }

    /// <summary>
    /// Gets or sets the attached files of the currently loaded document, or null when none is loaded.
    /// </summary>
    public PdfAttachmentsViewModel? Attachments { get; set; }

    /// <summary>
    /// Gets the outline items sent to JS, indexed by their handles.
    /// </summary>
    public List<PdfOutlineItemViewModel> OutlineItems { get; } = [];

    /// <summary>
    /// Gets the layer tree nodes of every layer configuration sent to JS, indexed by their handles.
    /// </summary>
    public List<PdfLayerViewModel> LayerItems { get; } = [];

    /// <summary>
    /// Gets or sets whether the layer configuration or the visibility of a layer changed since they were last sent to JS.
    /// </summary>
    public bool LayersChanged { get; set; }

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
    public PdfAnnotationPopup? AnnotationPopup { get; set; }

    /// <summary>
    /// Gets or sets the name of the content under the pointer last sent to JS, or null when none is shown.
    /// </summary>
    public string? ActiveContentName { get; set; }

    /// <summary>
    /// Gets or sets whether the search matches changed since they were last sent to JS.
    /// </summary>
    public bool SearchResultsChanged { get; set; }

    /// <summary>
    /// Gets or sets the range of the current search result last received from JS, or null when there is none.
    /// </summary>
    public PdfPanelTextRange? CurrentSearchRange { get; set; }

    /// <summary>
    /// Gets the actions the panel does not perform, requested since they were last sent to JS, in request order.
    /// </summary>
    public List<PdfPanelActionEventArgs> ActionRequests { get; } = [];

    /// <summary>
    /// Gets the embedded files sent to JS, indexed by their handles.
    /// </summary>
    public List<PdfFileSpecification> Files { get; } = [];

    /// <summary>
    /// Gets the handles of the embedded files sent to JS.
    /// </summary>
    public Dictionary<PdfFileSpecification, int> FileHandles { get; } = [];

    /// <summary>
    /// Gets or sets the credential offered for the next credential request of the document, or null to offer none.
    /// </summary>
    public PdfCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the last credential request of the document, or null when it requested none.
    /// </summary>
    public PdfCredentialRequest? LastCredentialRequest { get; set; }

    /// <summary>
    /// Keeps the credential request of the document and offers <see cref="Credential"/> when a credential is required;
    /// a rejected credential is not offered again.
    /// </summary>
    public PdfCredential? OnCredentialRequested(PdfCredentialRequest request)
    {
        LastCredentialRequest = request;
        return (request.Reason == PdfCredentialRequestReason.CredentialRequired) ? Credential : null;
    }

    /// <summary>
    /// Keeps an action the panel does not perform and schedules a redraw that sends it to JS.
    /// </summary>
    public void OnActionRequested(object? sender, PdfPanelActionEventArgs args)
    {
        ActionRequests.Add(args);
        PdfPanelInterop.ScheduleRedraw(ContainerId);
    }

    /// <summary>
    /// Marks the search matches as changed and schedules a redraw that sends them to JS.
    /// </summary>
    public void OnSearchMatchesChanged(object? sender, EventArgs args)
    {
        SearchResultsChanged = true;
        PdfPanelInterop.ScheduleRedraw(ContainerId);
    }

    /// <summary>
    /// Schedules a redraw that sends the extracted text state to JS.
    /// </summary>
    public void OnTextExtracted(object? sender, EventArgs args) => PdfPanelInterop.ScheduleRedraw(ContainerId);

    /// <summary>
    /// Marks the layers as changed when the layer configuration or the visibility of a layer changed.
    /// </summary>
    public void OnLayersPropertyChanged(object? sender, PropertyChangedEventArgs args) => LayersChanged = true;

    /// <summary>
    /// Passes the PNG of a requested thumbnail to JS when it is ready.
    /// </summary>
    public void OnThumbnailPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(PdfThumbnailViewModel.Png) || sender is not PdfThumbnailViewModel thumbnail)
        {
            return;
        }

        byte[]? png = thumbnail.Png;

        if (!thumbnail.IsRequested || png == null)
        {
            return;
        }

        PdfPanelInterop.ReportThumbnail(ContainerId, thumbnail.PageNumber, png);
    }
}
