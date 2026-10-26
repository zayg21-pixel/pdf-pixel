using Microsoft.Extensions.Logging;
using PdfPixel.Encryption;
using PdfPixel.Files;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Navigation;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Web.Dto.Converters;
using PdfPixel.PdfPanel.Web.Dto.Model;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.Rendering;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia.Fonts;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
internal static partial class PdfPanelInterop
{
    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = [];

    private static readonly ILoggerFactory LoggerFactory = new EmscriptenConsoleLoggerFactory(LogLevel.Warning);

    private static readonly ILogger Logger = LoggerFactory.CreateLogger(typeof(PdfPanelInterop));

    private static readonly PdfDocumentReader DocumentReader = new(LoggerFactory, new SkiaFontSubstitutor(LoggerFactory));

    private static bool _isInitialized;

    [JSExport]
    internal static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        Logger.LogInformation("PdfPanelInterop initialized");

        _isInitialized = true;
    }

    [JSExport]
    public static void RegisterPanel(string containerId, JSObject configuration)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (ResourcesMap.ContainsKey(containerId))
        {
            return;
        }

        try
        {
            bool webGl = configuration.GetPropertyAsBoolean("useWebGL");
            string selector = $"#{containerId} .pdf-panel-canvas";

            PdfPanelResources resources;

            if (webGl)
            {
                WebGlSkiaRenderer renderer = new(LoggerFactory.CreateLogger<WebGlSkiaRenderer>(), selector, frame => OnFramePresented(containerId, frame));
                resources = new PdfPanelResources(containerId, renderer, renderer);
            }
            else
            {
                CpuSkiaRenderer renderer = new(LoggerFactory.CreateLogger<CpuSkiaRenderer>(), selector, frame => OnFramePresented(containerId, frame));
                resources = new PdfPanelResources(containerId, renderer, renderer);
            }

            resources.YieldInterval = TimeSpan.FromMilliseconds(configuration.GetPropertyAsDouble("yieldInterval"));
            ReadConfiguration(configuration, resources.Configuration);

            ResourcesMap[containerId] = resources;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error registering container with id '{ContainerId}'", containerId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Updates the panel values set in <paramref name="configuration"/>,
    /// and applies them to the context of the loaded document.
    /// </summary>
    [JSExport]
    public static void UpdateConfiguration(string containerId, JSObject configuration)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources))
        {
            return;
        }

        try
        {
            ReadConfiguration(configuration, resources.Configuration);

            if (resources.Context != null)
            {
                resources.Configuration.ApplyConfiguration(resources.Context);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating configuration of container '{Id}'", containerId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Returns the selected content of the given container: <c>{ type: 'image', png }</c> for a selected image,
    /// <c>{ type: 'text', text }</c> for selected text, or null when nothing is selected.
    /// </summary>
    [JSExport]
    public static JSObject? GetSelectedContent(string containerId)
    {
        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources) || resources.Context == null)
        {
            return null;
        }

        PdfPanelImageContentItem? selectedImage = resources.Context.Content.SelectedImage;

        if (selectedImage != null)
        {
            PdfCancellationExecutionObserver observer = new(CancellationToken.None);
            byte[] png = selectedImage.Image.DecodePng(PdfImageDecodingParameters.Default, observer);

            return CreateSelectedImage(png);
        }

        string text = resources.Context.Text.SelectedText;

        if (text.Length == 0)
        {
            return null;
        }

        return CreateSelectedText(text);
    }

    [JSExport]
    public static void UnregisterPanel(string containerId)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources))
        {
            DisposeContext(resources);
            resources.Document?.Dispose();
            resources.Document?.Document.Dispose();
            resources.SkSurfaceFactory.Dispose();

            ResourcesMap.Remove(containerId);
        }
    }

    /// <summary>
    /// Loads <paramref name="document"/> into the panel of <paramref name="containerId"/>, opening an encrypted document
    /// with the JS <paramref name="credential"/>, and returns the JSON result; the shown document is kept when
    /// loading fails.
    /// </summary>
    [JSExport]
    internal static string SetDocument(string containerId, byte[] document, JSObject? credential)
    {
        if (!_isInitialized)
        {
            return DocumentDtoConverter.CreateDocumentError("error", "The runtime is not initialized.");
        }

        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources))
        {
            Logger.LogWarning("Received document data for unknown container id '{Id}'", containerId);
            return DocumentDtoConverter.CreateDocumentError("error", $"No panel is registered with id '{containerId}'.");
        }

        if (document == null || document.Length == 0)
        {
            return DocumentDtoConverter.CreateDocumentError("empty", "The document data is empty.");
        }

        IPdfDocument loadedDocument;
        resources.Credential = ReadCredential(credential);
        resources.LastCredentialRequest = null;

        try
        {
            Logger.LogInformation("Reading PDF document, size={Size} bytes", document.Length);
            loadedDocument = DocumentReader.Read(new MemoryStream(document), resources.OnCredentialRequested);
        }
        catch (PdfAuthenticationException ex)
        {
            Logger.LogInformation("PDF document for container '{Id}' is not opened: {Message}", containerId, ex.Message);
            return DocumentDtoConverter.CreateCredentialRequestedResult("encrypted", ex.Message, resources.LastCredentialRequest);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for container '{Id}'", containerId);
            return DocumentDtoConverter.CreateDocumentError(GetDocumentErrorReason(ex), ex.Message);
        }
#pragma warning restore CA1031
        finally
        {
            resources.Credential = null;
        }

        try
        {
            resources.Document?.Dispose();
            resources.Document?.Document.Dispose();

            resources.Document = PdfPanelDocument.FromDocument(
                loadedDocument,
                new SequentialWorkQueue(LoggerFactory.CreateLogger<SequentialWorkQueue>()),
                new PdfYieldingObserverFactory(resources.YieldInterval),
                SynchronizationContext.Current);

            Logger.LogInformation("PDF document parsed, pages={PageCount}", resources.Document.Pages.Count);

            DisposeContext(resources);
            resources.Context = new PdfPanelContext(resources.Document, resources.SkSurfaceFactory, resources.RenderTargetFactory);
            resources.Configuration.ApplyConfiguration(resources.Context);
            resources.Context.Actions.ActionRequested += resources.OnActionRequested;
            resources.Context.Search.MatchesChanged += resources.OnSearchMatchesChanged;
            resources.Context.Text.TextExtracted += resources.OnTextExtracted;
            resources.SearchResultsChanged = true;
            resources.CurrentSearchRange = null;

            CreateViewModels(resources, resources.Document, resources.Context);

            return DocumentDtoConverter.CreateDocumentResult(resources);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing PDF document for container '{Id}'", containerId);
            return DocumentDtoConverter.CreateDocumentError("error", ex.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Scrolls the panel of <paramref name="containerId"/> to a page and view given by the JS destination object
    /// on the next redraw.
    /// </summary>
    [JSExport]
    public static bool ScrollToDestination(string containerId, JSObject destination)
    {
        if (!TryGetContext(containerId, out PdfPanelContext? context))
        {
            return false;
        }

        PdfPageNumberDestination? pageDestination = ReadDestination(destination);

        if (pageDestination == null)
        {
            Logger.LogWarning("Unknown destination fit type '{FitType}'", destination.GetPropertyAsString("fitType"));
            return false;
        }

        context.Actions.ScrollToDestination(pageDestination);
        return true;
    }

    /// <summary>
    /// Rotates one page of the panel of <paramref name="containerId"/> by <paramref name="degrees"/>, keeping the current page in view.
    /// </summary>
    [JSExport]
    public static bool RotatePage(string containerId, int pageNumber, int degrees)
    {
        if (degrees % 90 != 0 || !TryGetContext(containerId, out PdfPanelContext? context))
        {
            return false;
        }

        if (!context.Document.TryGetPage(pageNumber, out PdfPanelPage? page) || page == null)
        {
            return false;
        }

        int currentPage = context.GetCurrentPage();
        page.Rotation += degrees;
        context.Actions.ScrollToPage(currentPage);
        return true;
    }

    /// <summary>
    /// Rotates every page of the panel of <paramref name="containerId"/> by <paramref name="degrees"/>, keeping the current page in view.
    /// </summary>
    [JSExport]
    public static bool RotatePages(string containerId, int degrees)
    {
        if (degrees % 90 != 0 || !TryGetContext(containerId, out PdfPanelContext? context))
        {
            return false;
        }

        int currentPage = context.GetCurrentPage();

        foreach (PdfPanelPage page in context.Document.Pages)
        {
            page.Rotation += degrees;
        }

        context.Actions.ScrollToPage(currentPage);
        return true;
    }

    /// <summary>
    /// Performs the action of the outline item with <paramref name="handle"/>, or navigates to its destination
    /// when it has no action, on the next redraw.
    /// </summary>
    [JSExport]
    public static bool ActivateOutlineItem(string containerId, int handle)
    {
        if (!TryGetResources(containerId, out PdfPanelResources? resources) || resources.Outlines == null)
        {
            return false;
        }

        if (handle < 0 || handle >= resources.OutlineItems.Count)
        {
            return false;
        }

        resources.Outlines.Activate(resources.OutlineItems[handle]);
        return true;
    }

    /// <summary>
    /// Renders content with the layer configuration of <paramref name="handle"/>.
    /// </summary>
    [JSExport]
    public static bool SetLayerConfiguration(string containerId, int handle)
    {
        if (!TryGetResources(containerId, out PdfPanelResources? resources) || resources.Layers == null)
        {
            return false;
        }

        if (handle < 0 || handle >= resources.Layers.Configurations.Count)
        {
            return false;
        }

        resources.Layers.Configuration = resources.Layers.Configurations[handle];
        return true;
    }

    /// <summary>
    /// Shows or hides the layer of <paramref name="handle"/>; showing it hides the other layers of its radio-button sets.
    /// </summary>
    [JSExport]
    public static bool SetLayerVisible(string containerId, int handle, bool visible)
    {
        if (!TryGetResources(containerId, out PdfPanelResources? resources))
        {
            return false;
        }

        if (handle < 0 || handle >= resources.LayerItems.Count || !resources.LayerItems[handle].IsLayer)
        {
            return false;
        }

        resources.LayerItems[handle].IsVisible = visible;
        return true;
    }

    /// <summary>
    /// Returns the JS result of reading the embedded file of <paramref name="handle"/>, decrypting it with the JS
    /// <paramref name="credential"/> when it requires one, or null when there is no such file.
    /// </summary>
    [JSExport]
    public static JSObject? GetFileBytes(string containerId, int handle, JSObject? credential)
    {
        if (!TryGetResources(containerId, out PdfPanelResources? resources) || resources.Document == null)
        {
            return null;
        }

        if (handle < 0 || handle >= resources.Files.Count)
        {
            return null;
        }

        PdfFileSpecification fileSpecification = resources.Files[handle];
        PdfEmbeddedFile? embeddedFile = fileSpecification.UnicodeEmbeddedFile ?? fileSpecification.EmbeddedFile;

        if (embeddedFile == null)
        {
            return null;
        }

        resources.Credential = ReadCredential(credential);
        resources.LastCredentialRequest = null;

        try
        {
            lock (resources.Document.DocumentLocker)
            {
                return CreateFileResult(embeddedFile.Stream.DecodeAsMemory().ToArray());
            }
        }
        catch (PdfAuthenticationException ex)
        {
            Logger.LogInformation("Embedded file of container '{Id}' is not opened: {Message}", containerId, ex.Message);
            return CreateCredentialRequestedResult("encrypted", ex.Message, resources.LastCredentialRequest);
        }
        finally
        {
            resources.Credential = null;
        }
    }

    /// <summary>
    /// Returns the JSON thumbnail layout <c>[{ pageNumber, width, height, rotation }]</c> of the pages of the panel of
    /// <paramref name="containerId"/>, or <see langword="null"/> when the panel has no document.
    /// </summary>
    [JSExport]
    public static string? GetThumbnailLayout(string containerId)
    {
        if (!TryGetContext(containerId, out PdfPanelContext? context))
        {
            return null;
        }

        return ThumbnailDtoConverter.CreateLayout(context);
    }

    /// <summary>
    /// Returns the PNG of the thumbnail of a page, or <see langword="null"/> when it has not been produced.
    /// </summary>
    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static byte[]? GetThumbnail(string containerId, int pageNumber)
    {
        if (!TryGetContext(containerId, out PdfPanelContext? context) || pageNumber < 1 || pageNumber > context.Document.Pages.Count)
        {
            return null;
        }

        return context.Thumbnails.GetPng(pageNumber);
    }

    /// <summary>
    /// Starts producing the thumbnail of a page that has none; its PNG is passed to the thumbnail subscriber of the panel
    /// when it is ready, and again whenever it is produced anew while it stays requested. Does nothing for a page whose
    /// thumbnail is already produced.
    /// </summary>
    [JSExport]
    public static bool RequestThumbnail(string containerId, int pageNumber)
        => SetThumbnailRequested(containerId, pageNumber, true);

    /// <summary>
    /// Cancels producing the thumbnail of a page; no PNG of it is passed until it is requested again. Does nothing for a
    /// page whose thumbnail is not requested.
    /// </summary>
    [JSExport]
    public static bool CancelThumbnail(string containerId, int pageNumber)
        => SetThumbnailRequested(containerId, pageNumber, false);

    /// <summary>
    /// Passes the PNG of the thumbnail of a page to the thumbnail subscriber of the panel of <paramref name="containerId"/>.
    /// </summary>
    internal static void ReportThumbnail(string containerId, int pageNumber, byte[] png)
        => ThumbnailUpdated(containerId, pageNumber, png);

    /// <summary>
    /// Applies the JSON view state <paramref name="input"/> to the panel of <paramref name="containerId"/>, renders it
    /// and returns the JSON view state after the render, or <see langword="null"/> when the panel has no document.
    /// </summary>
    [JSExport]
    public static string? RequestRedraw(string containerId, string input)
    {
        if (!_isInitialized)
        {
            return null;
        }

        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources) || resources.Context == null)
        {
            return null;
        }

        string? output = null;

        try
        {
            RedrawInputDto? state = RedrawDtoConverter.ReadInput(input);

            if (state == null)
            {
                return null;
            }

            resources.Context.VerticalOffset = state.VerticalOffset;
            resources.Context.HorizontalOffset = state.HorizontalOffset;
            resources.Context.Scale = state.Scale;
            resources.Context.AutoScaleMode = RedrawDtoConverter.ReadAutoScaleMode(state.AutoScaleMode) ?? PdfPanelAutoScaleMode.NoAutoScale;
            resources.Context.PanelWidth = state.PanelWidth;
            resources.Context.PanelHeight = state.PanelHeight;

            ApplyZoomRequest(resources.Context, state.ZoomRequest);

            if (state.ForcePageSet > 0)
            {
                resources.Context.ScrollToPage(state.ForcePageSet);
            }

            resources.Context.Search.Query = state.SearchQuery;
            ApplyCurrentSearchResult(resources, resources.Context, state.CurrentSearchResult);

            if (state.PointerInside)
            {
                resources.Context.Input.PointerPosition = new PdfPoint(state.PointerX, state.PointerY);
            }
            else
            {
                resources.Context.Input.PointerPosition = null;
            }

            resources.Context.Input.PointerState = (state.PointerPressed) ?PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            resources.Context.Synchronize();
            resources.Layers?.Synchronize();

            output = RedrawDtoConverter.CreateResult(resources, resources.Context);

            resources.Context.Render();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in container '{Id}'", containerId);
        }
#pragma warning restore CA1031

        return output;
    }

    /// <summary>
    /// Zooms <paramref name="context"/> by the <c>zoomRequest</c> of the redraw state: to its <c>scale</c> when set,
    /// otherwise one step in or out by the sign of its <c>step</c>, around its <c>centerX</c> and <c>centerY</c>.
    /// </summary>
    private static void ApplyZoomRequest(PdfPanelContext context, ZoomRequestDto? zoomRequest)
    {
        if (zoomRequest == null)
        {
            return;
        }

        if (zoomRequest.Scale != null)
        {
            context.Zoom(zoomRequest.Scale.Value, zoomRequest.CenterX, zoomRequest.CenterY);
        }
        else if (zoomRequest.Step > 0)
        {
            context.ZoomIn(zoomRequest.CenterX, zoomRequest.CenterY);
        }
        else
        {
            context.ZoomOut(zoomRequest.CenterX, zoomRequest.CenterY);
        }
    }

    /// <summary>
    /// Unsubscribes the panel from the search of its context and disposes the context.
    /// </summary>
    private static void DisposeContext(PdfPanelResources resources)
    {
        if (resources.Context == null)
        {
            return;
        }

        resources.Context.Actions.ActionRequested -= resources.OnActionRequested;
        resources.Context.Search.MatchesChanged -= resources.OnSearchMatchesChanged;
        resources.Context.Text.TextExtracted -= resources.OnTextExtracted;

        if (resources.Thumbnails != null)
        {
            foreach (PdfThumbnailViewModel thumbnail in resources.Thumbnails.Items)
            {
                thumbnail.PropertyChanged -= resources.OnThumbnailPropertyChanged;
            }
        }

        if (resources.Layers != null)
        {
            resources.Layers.PropertyChanged -= resources.OnLayersPropertyChanged;
        }

        foreach (PdfLayerViewModel layer in resources.LayerItems)
        {
            layer.PropertyChanged -= resources.OnLayersPropertyChanged;
        }

        resources.Outlines = null;
        resources.Layers = null;
        resources.Thumbnails = null;
        resources.Attachments = null;
        resources.OutlineItems.Clear();
        resources.ActionRequests.Clear();
        resources.Files.Clear();
        resources.FileHandles.Clear();
        resources.LayerItems.Clear();
        resources.Context.Dispose();
    }

    /// <summary>
    /// Creates the view models of the outline, layers, thumbnails and attached files of the loaded document
    /// and subscribes the panel to the changes it sends to JS.
    /// </summary>
    private static void CreateViewModels(PdfPanelResources resources, PdfPanelDocument document, PdfPanelContext context)
    {
        resources.Outlines = new PdfOutlinesViewModel(document, context.Actions);
        resources.Layers = new PdfLayersViewModel(context.Layers);
        resources.Thumbnails = new PdfThumbnailsViewModel(context.Thumbnails, document, context.Actions);
        resources.Attachments = new PdfAttachmentsViewModel(document);
        resources.LayersChanged = true;

        resources.Layers.PropertyChanged += resources.OnLayersPropertyChanged;

        foreach (PdfThumbnailViewModel thumbnail in resources.Thumbnails.Items)
        {
            thumbnail.PropertyChanged += resources.OnThumbnailPropertyChanged;
        }
    }

    /// <summary>
    /// Sets the current search match of <paramref name="context"/> from the <c>currentSearchResult</c> range of the redraw state,
    /// and scrolls to it when the range differs from the last one received.
    /// </summary>
    private static void ApplyCurrentSearchResult(PdfPanelResources resources, PdfPanelContext context, TextRangeDto? currentSearchResult)
    {
        PdfPanelTextRange? range = RedrawDtoConverter.ReadTextRange(currentSearchResult);
        PdfPanelSearchMatch? currentMatch = null;

        if (range != null)
        {
            foreach (PdfPanelSearchMatch match in context.Search.Matches)
            {
                if (match.Range == range.Value)
                {
                    currentMatch = match;
                    break;
                }
            }
        }

        context.Search.CurrentMatch = currentMatch;

        if (range == resources.CurrentSearchRange)
        {
            return;
        }

        resources.CurrentSearchRange = range;

        if (currentMatch != null)
        {
            context.ScrollToSearchMatch(currentMatch.Value);
        }
    }

    private static bool TryGetContext(string containerId, [NotNullWhen(true)] out PdfPanelContext? context)
    {
        context = null;

        if (!_isInitialized || !ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources) || resources.Context == null)
        {
            return false;
        }

        context = resources.Context;
        return true;
    }

    private static bool TryGetResources(string containerId, [NotNullWhen(true)] out PdfPanelResources? resources)
    {
        if (!_isInitialized || !ResourcesMap.TryGetValue(containerId, out resources) || resources.Context == null)
        {
            resources = null;
            return false;
        }

        return true;
    }

    private static bool SetThumbnailRequested(string containerId, int pageNumber, bool isRequested)
    {
        if (!TryGetResources(containerId, out PdfPanelResources? resources) || resources.Thumbnails == null || resources.Context == null)
        {
            return false;
        }

        IReadOnlyList<PdfThumbnailViewModel> thumbnails = resources.Thumbnails.Items;

        if (pageNumber < 1 || pageNumber > thumbnails.Count)
        {
            return false;
        }

        if (isRequested && resources.Context.Thumbnails.GetPng(pageNumber) != null)
        {
            return false;
        }

        thumbnails[pageNumber - 1].IsRequested = isRequested;
        return true;
    }

    /// <summary>
    /// Builds the JSON of <paramref name="frame"/> and passes it to the view of <paramref name="containerId"/>.
    /// </summary>
    private static void OnFramePresented(string containerId, PdfPanelFrame frame)
    {
        string frameJson = FrameDtoConverter.CreateFrame(frame);
        FramePresented(containerId, frameJson);
    }

    [JSImport("framePresented", "canvasInterop.js")]
    private static partial void FramePresented(string containerId, string frame);

    [JSImport("scheduleRedraw", "canvasInterop.js")]
    internal static partial void ScheduleRedraw(string containerId);

    [JSImport("thumbnailUpdated", "canvasInterop.js")]
    private static partial void ThumbnailUpdated(string containerId, int pageNumber, [JSMarshalAs<JSType.Array<JSType.Number>>] byte[] png);
}
