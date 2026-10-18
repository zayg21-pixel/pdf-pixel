using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.Rendering;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia.Fonts;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            PdfPanelJsConverter.ReadConfiguration(configuration, resources.Configuration);

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
            PdfPanelJsConverter.ReadConfiguration(configuration, resources.Configuration);

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

            JSObject image = CreateObject();
            image.SetProperty("type", "image");
            image.SetProperty("png", png);

            return image;
        }

        string text = resources.Context.Text.SelectedText;

        if (text.Length == 0)
        {
            return null;
        }

        JSObject content = CreateObject();
        content.SetProperty("type", "text");
        content.SetProperty("text", text);

        return content;
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
            resources.Pages?.Dispose();
            resources.Document?.Dispose();
            resources.SkSurfaceFactory.Dispose();

            ResourcesMap.Remove(containerId);
        }
    }

    /// <summary>
    /// Loads <paramref name="document"/> into the panel of <paramref name="containerId"/> and returns the JS result object;
    /// the shown document is kept when loading fails.
    /// </summary>
    [JSExport]
    internal static JSObject SetDocument(string containerId, byte[] document)
    {
        JSObject result = CreateObject();

        if (!_isInitialized)
        {
            PdfPanelJsConverter.WriteDocumentResult(result, "error", "The runtime is not initialized.");
            return result;
        }

        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources))
        {
            Logger.LogWarning("Received document data for unknown container id '{Id}'", containerId);
            PdfPanelJsConverter.WriteDocumentResult(result, "error", $"No panel is registered with id '{containerId}'.");
            return result;
        }

        if (document == null || document.Length == 0)
        {
            PdfPanelJsConverter.WriteDocumentResult(result, "empty", "The document data is empty.");
            return result;
        }

        IPdfDocument loadedDocument;

        try
        {
            Logger.LogInformation("Reading PDF document, size={Size} bytes", document.Length);
            loadedDocument = DocumentReader.Read(new MemoryStream(document));
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for container '{Id}'", containerId);
            PdfPanelJsConverter.WriteDocumentResult(result, PdfPanelJsConverter.GetDocumentErrorReason(ex), ex.Message);
            return result;
        }
#pragma warning restore CA1031

        try
        {
            resources.Pages?.Dispose();
            resources.Document?.Dispose();

            resources.Document = loadedDocument;

            resources.Pages = PdfPanelPageCollection.FromDocument(
                resources.Document,
                new SequentialWorkQueue(LoggerFactory.CreateLogger<SequentialWorkQueue>()),
                new PdfYieldingObserverFactory(resources.YieldInterval),
                SynchronizationContext.Current);

            Logger.LogInformation("PDF document parsed, pages={PageCount}", resources.Pages.Count);

            DisposeContext(resources);
            resources.Context = new PdfPanelContext(resources.Pages, resources.SkSurfaceFactory, resources.RenderTargetFactory);
            resources.Configuration.ApplyConfiguration(resources.Context);
            resources.Context.Actions.ActionRequested += resources.OnActionRequested;
            resources.Context.Search.MatchesChanged += resources.OnSearchMatchesChanged;
            resources.Context.Text.TextExtracted += resources.OnTextExtracted;
            resources.SearchResultsChanged = true;
            resources.CurrentSearchRange = null;

            PdfPanelJsConverter.WriteDocumentResult(result, resources.Pages.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error showing PDF document for container '{Id}'", containerId);
            PdfPanelJsConverter.WriteDocumentResult(result, "error", ex.Message);
        }
#pragma warning restore CA1031

        return result;
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

        PdfPageNumberDestination? pageDestination = PdfPanelJsConverter.ReadDestination(destination);

        if (pageDestination == null)
        {
            Logger.LogWarning("Unknown destination fit type '{FitType}'", destination.GetPropertyAsString("fitType"));
            return false;
        }

        context.Actions.ScrollToDestination(pageDestination);
        return true;
    }

    /// <summary>
    /// Sets the rotation of one page of the panel of <paramref name="containerId"/>, keeping the current page in view.
    /// </summary>
    [JSExport]
    public static bool SetPageRotation(string containerId, int pageNumber, int degrees)
    {
        if (degrees % 90 != 0 || !TryGetContext(containerId, out PdfPanelContext? context))
        {
            return false;
        }

        if (!context.Pages.TryGetPage(pageNumber, out PdfPanelPage? page) || page == null)
        {
            return false;
        }

        int currentPage = context.GetCurrentPage();
        page.UserRotation = degrees;
        context.Actions.ScrollToPage(currentPage);
        return true;
    }

    /// <summary>
    /// Sets the rotation of every page of the panel of <paramref name="containerId"/>, keeping the current page in view.
    /// </summary>
    [JSExport]
    public static bool SetRotation(string containerId, int degrees)
    {
        if (degrees % 90 != 0 || !TryGetContext(containerId, out PdfPanelContext? context))
        {
            return false;
        }

        int currentPage = context.GetCurrentPage();

        foreach (PdfPanelPage page in context.Pages)
        {
            page.UserRotation = degrees;
        }

        context.Actions.ScrollToPage(currentPage);
        return true;
    }

    [JSExport]
    public static void RequestRedraw(string containerId, JSObject state)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(containerId, out PdfPanelResources? resources) || resources.Context == null)
        {
            return;
        }

        try
        {
            int width = state.GetPropertyAsInt32("panelWidth");
            int height = state.GetPropertyAsInt32("panelHeight");

            var verticalOffset = (float)(double)state.GetPropertyAsDouble("verticalOffset");
            var horizontalOffset = (float)(double)state.GetPropertyAsDouble("horizontalOffset");
            var scale = (float)(double)state.GetPropertyAsDouble("scale");

            resources.Context.VerticalOffset = verticalOffset;
            resources.Context.HorizontalOffset = horizontalOffset;
            resources.Context.Scale = scale;
            resources.Context.AutoScaleMode = PdfPanelJsConverter.ReadAutoScaleMode(state.GetPropertyAsString("autoScaleMode")) ?? PdfPanelAutoScaleMode.NoAutoScale;
            resources.Context.PanelWidth = width;
            resources.Context.PanelHeight = height;

            ApplyZoomRequest(resources.Context, state);

            int forcePageSet = state.GetPropertyAsInt32("forcePageSet");
            if (forcePageSet > 0)
            {
                resources.Context.ScrollToPage(forcePageSet);
            }

            resources.Context.Search.Query = state.GetPropertyAsString("searchQuery");
            ApplyCurrentSearchResult(resources, resources.Context, state);

            bool pointerInside = state.GetPropertyAsBoolean("pointerInside");
            if (pointerInside)
            {
                var pointerX = (float)(double)state.GetPropertyAsDouble("pointerX");
                var pointerY = (float)(double)state.GetPropertyAsDouble("pointerY");
                resources.Context.Input.PointerPosition = new PdfPoint(pointerX, pointerY);
            }
            else
            {
                resources.Context.Input.PointerPosition = null;
            }

            bool pointerPressed = state.GetPropertyAsBoolean("pointerPressed");
            resources.Context.Input.PointerState = pointerPressed ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            resources.Context.Synchronize();

            state.SetProperty("cursorStyle", PdfPanelJsConverter.ToCursorStyle(resources.Context.Input.Cursor));
            state.SetProperty("openUri", resources.OpenUri);
            resources.OpenUri = string.Empty;

            PdfAnnotationPopup? activeAnnotation = resources.Context.Annotations.ActiveAnnotation;

            if (activeAnnotation != resources.AnnotationPopup)
            {
                resources.AnnotationPopup = activeAnnotation;
                SetAnnotationPopup(state, activeAnnotation);
            }

            string? activeContentName = resources.Context.Content.ActiveContentName;

            if (activeContentName != resources.ActiveContentName)
            {
                resources.ActiveContentName = activeContentName;
                state.SetProperty("activeContentNameChanged", true);
                state.SetProperty("activeContentName", activeContentName);
            }

            PdfPanelSearch search = resources.Context.Search;
            state.SetProperty("isTextExtracted", resources.Context.Text.IsTextExtracted);

            if (resources.SearchResultsChanged)
            {
                resources.SearchResultsChanged = false;
                SetSearchResults(state, search.Matches);
            }

            state.SetProperty("scrollWidth", resources.Context.ExtentWidth);
            state.SetProperty("scrollHeight", resources.Context.ExtentHeight);
            state.SetProperty("verticalOffset", resources.Context.VerticalOffset);
            state.SetProperty("horizontalOffset", resources.Context.HorizontalOffset);
            state.SetProperty("scale", resources.Context.Scale);
            state.SetProperty("autoScaleMode", PdfPanelJsConverter.ToString(resources.Context.AutoScaleMode));
            state.SetProperty("currentPage", resources.Context.GetCurrentPage());
            state.SetProperty("pageCount", resources.Context.Pages.Count);

            resources.Context.Render();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in container '{Id}'", containerId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Zooms <paramref name="context"/> by the <c>zoomRequest</c> of the redraw state: to its <c>scale</c> when set,
    /// otherwise one step in or out by the sign of its <c>step</c>, around its <c>centerX</c> and <c>centerY</c>.
    /// </summary>
    private static void ApplyZoomRequest(PdfPanelContext context, JSObject state)
    {
        using JSObject? zoomRequest = state.GetPropertyAsJSObject("zoomRequest");

        if (zoomRequest == null)
        {
            return;
        }

        var centerX = (float)zoomRequest.GetPropertyAsDouble("centerX");
        var centerY = (float)zoomRequest.GetPropertyAsDouble("centerY");

        if (zoomRequest.HasProperty("scale"))
        {
            context.Zoom((float)zoomRequest.GetPropertyAsDouble("scale"), centerX, centerY);
        }
        else if (zoomRequest.GetPropertyAsInt32("step") > 0)
        {
            context.ZoomIn(centerX, centerY);
        }
        else
        {
            context.ZoomOut(centerX, centerY);
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
        resources.Context.Dispose();
    }

    /// <summary>
    /// Sets the current search match of <paramref name="context"/> from the <c>currentSearchResult</c> range of the redraw state,
    /// and scrolls to it when the range differs from the last one received.
    /// </summary>
    private static void ApplyCurrentSearchResult(PdfPanelResources resources, PdfPanelContext context, JSObject state)
    {
        PdfPanelTextRange? range;

        using (JSObject? result = state.GetPropertyAsJSObject("currentSearchResult"))
        {
            range = PdfPanelJsConverter.ReadTextRange(result);
        }

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

    /// <summary>
    /// Sets the JS array built from <paramref name="matches"/> and marks the search results as changed on the redraw state.
    /// </summary>
    private static void SetSearchResults(JSObject state, IReadOnlyList<PdfPanelSearchMatch> matches)
    {
        state.SetProperty("searchResultsChanged", true);

        using JSObject results = CreateArray();

        for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            PdfPanelSearchMatch match = matches[matchIndex];

            using JSObject result = CreateObject();
            using JSObject bounds = CreateObject();
            PdfPanelJsConverter.WriteSearchMatch(result, match);
            PdfPanelJsConverter.WriteRectangle(bounds, match.Bounds);
            result.SetProperty("bounds", bounds);

            results.SetProperty(matchIndex.ToString(CultureInfo.InvariantCulture), result);
        }

        state.SetProperty("searchResults", results);
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

    /// <summary>
    /// Sets the JS popup object built from <paramref name="popup"/>, or null when no annotation is active,
    /// and marks the popup as changed on the redraw state.
    /// </summary>
    private static void SetAnnotationPopup(JSObject state, PdfAnnotationPopup? popup)
    {
        state.SetProperty("annotationPopupChanged", true);

        if (popup == null)
        {
            state.SetProperty("annotationPopup", (JSObject?)null);
            return;
        }

        using JSObject popupObject = CreateObject();
        using JSObject messagesArray = CreateMessagesArray(popup.Messages);
        popupObject.SetProperty("isInteractive", popup.IsInteractive);
        popupObject.SetProperty("messages", messagesArray);

        state.SetProperty("annotationPopup", popupObject);
    }

    /// <summary>
    /// Builds the JS array of <paramref name="messages"/>, each with its replies nested.
    /// </summary>
    private static JSObject CreateMessagesArray(IReadOnlyList<PdfAnnotationMessage> messages)
    {
        JSObject messagesArray = CreateArray();

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            PdfAnnotationMessage message = messages[messageIndex];

            using JSObject messageObject = CreateObject();
            using JSObject repliesArray = CreateMessagesArray(message.Replies);
            PdfPanelJsConverter.WriteAnnotationMessage(messageObject, message);
            messageObject.SetProperty("replies", repliesArray);

            messagesArray.SetProperty(messageIndex.ToString(CultureInfo.InvariantCulture), messageObject);
        }

        return messagesArray;
    }

    /// <summary>
    /// Builds the JS object of <paramref name="frame"/> and passes it to the view of <paramref name="containerId"/>.
    /// </summary>
    private static void OnFramePresented(string containerId, PdfPanelFrame frame)
    {
        using JSObject frameObject = CreateFrame(frame.PanelSize.Width, frame.PanelSize.Height, ToDomMatrix(frame.HostToPanel));

        foreach (PdfPanelFramePage page in frame.Pages)
        {
            PdfRectangle cropBox = page.Info.CropBox;
            PdfSize rotatedSize = page.RotatedSize;
            double[] cropBoxValues = [cropBox.Left, cropBox.Top, cropBox.Right, cropBox.Bottom];
            double[] rotatedSizeValues = [rotatedSize.Width, rotatedSize.Height];

            AddFramePage(
                frameObject,
                page.PageNumber,
                page.Info.Label,
                cropBoxValues,
                page.Info.Rotation,
                page.UserRotation,
                rotatedSizeValues,
                ToDomMatrix(page.GetPanelToPage(0)));
        }

        FramePresented(containerId, frameObject);
    }

    /// <summary>
    /// Returns the <c>DOMMatrix</c> component order <c>[a, b, c, d, e, f]</c> of <paramref name="matrix"/>.
    /// </summary>
    private static double[] ToDomMatrix(in PdfMatrix matrix)
        => [matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY];

    [JSImport("createFrame", "canvasInterop.js")]
    private static partial JSObject CreateFrame(double panelWidth, double panelHeight, [JSMarshalAs<JSType.Array<JSType.Number>>] double[] hostToPanel);

    [JSImport("addFramePage", "canvasInterop.js")]
    private static partial void AddFramePage(
        JSObject frame,
        int pageNumber,
        string label,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] cropBox,
        int rotation,
        int userRotation,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] rotatedSize,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] panelToContent);

    [JSImport("framePresented", "canvasInterop.js")]
    private static partial void FramePresented(string containerId, JSObject frame);

    [JSImport("scheduleRedraw", "canvasInterop.js")]
    internal static partial void ScheduleRedraw(string containerId);

    [JSImport("createObject", "canvasInterop.js")]
    private static partial JSObject CreateObject();

    [JSImport("createArray", "canvasInterop.js")]
    private static partial JSObject CreateArray();
}
