using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
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
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public partial class PdfPanelInterop
{
    private static bool _isInitialized = false;

    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = new();

    private static PdfDocumentReader DocumentReader;

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

    [JSExport]
    internal static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddEmscriptenConsole(LogLevel.Warning));
        Logger = LoggerFactory.CreateLogger<PdfPanelInterop>();
        DocumentReader = new PdfDocumentReader(LoggerFactory, new SkiaFontSubstitutor(LoggerFactory));
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
            var webGl = configuration.GetPropertyAsBoolean("useWebGL");
            var selector = $"#{containerId} .pdf-panel-canvas";

            PdfPanelResources resources;

            if (webGl)
            {
                var renderer = new WebGlSkiaRenderer(LoggerFactory.CreateLogger<WebGlSkiaRenderer>(), selector, frame => OnFramePresented(containerId, frame));
                resources = new PdfPanelResources(containerId)
                {
                    SkSurfaceFactory = renderer,
                    RenderTargetFactory = renderer
                };
            }
            else
            {
                var renderer = new CpuSkiaRenderer(LoggerFactory.CreateLogger<CpuSkiaRenderer>(), selector, frame => OnFramePresented(containerId, frame));
                resources = new PdfPanelResources(containerId)
                {
                    SkSurfaceFactory = renderer,
                    RenderTargetFactory = renderer
                };
            }

            resources.YieldInterval = TimeSpan.FromMilliseconds(configuration.GetPropertyAsDouble("yieldInterval"));
            ReadConfiguration(configuration, resources.Configuration);

            ResourcesMap[containerId] = resources;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error registering container with id '{ContainerId}'", containerId);
        }
    }

    /// <summary>
    /// Updates the panel values set in the <c>settings</c> object of <paramref name="configuration"/>,
    /// and applies them to the context of the loaded document.
    /// </summary>
    [JSExport]
    public static void UpdateConfiguration(string containerId, JSObject configuration)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(containerId, out var resources))
        {
            return;
        }

        try
        {
            ReadConfiguration(configuration, resources.Configuration);

            if (resources.Context != null)
            {
                resources.Configuration.Apply(resources.Context);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating configuration of container '{Id}'", containerId);
        }
    }

    /// <summary>
    /// Returns the currently selected text for the given container, or an empty string if nothing is selected.
    /// </summary>
    [JSExport]
    public static string GetSelectedText(string containerId)
    {
        if (!ResourcesMap.TryGetValue(containerId, out var resources) || resources.Context == null)
        {
            return string.Empty;
        }

        return resources.Context.Text.SelectedText;
    }

    [JSExport]
    public static void UnregisterPanel(string containerId)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (ResourcesMap.TryGetValue(containerId, out var resources))
        {
            DisposeContext(resources);
            resources.Pages?.Dispose();
            resources.Document?.Dispose();
            resources.SkSurfaceFactory.Dispose();

            ResourcesMap.Remove(containerId);
        }
    }

    [JSExport]
    internal static void SetDocument(string containerId, byte[] document)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(containerId, out var resources))
        {
            Logger.LogWarning("Received document data for unknown container id '{Id}'", containerId);
            return;
        }

        try
        {
            Logger.LogInformation("Reading PDF document, size={Size} bytes", document.Length);

            resources.Pages?.Dispose();
            resources.Document?.Dispose();

            resources.Document = DocumentReader.Read(new MemoryStream(document));

            resources.Pages = PdfPanelPageCollection.FromDocument(
                resources.Document,
                new SequentialWorkQueue(LoggerFactory.CreateLogger<SequentialWorkQueue>()),
                new PdfYieldingObserverFactory(resources.YieldInterval));

            Logger.LogInformation("PDF document parsed, pages={PageCount}", resources.Pages.Count);

            DisposeContext(resources);
            resources.Context = new PdfPanelContext(resources.Pages, resources.SkSurfaceFactory, resources.RenderTargetFactory, SynchronizationContext.Current);
            resources.Configuration.Apply(resources.Context);
            resources.Context.Actions.UriRequested += resources.OnUriRequested;
            resources.Context.Actions.RemoteDocumentRequested += resources.OnRemoteDocumentRequested;
            resources.Context.Search.MatchesChanged += resources.OnSearchMatchesChanged;
            resources.Context.Text.TextExtracted += resources.OnTextExtracted;
            resources.SearchResultsChanged = true;
            resources.CurrentSearchRange = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for container '{Id}'", containerId);
        }
    }

    [JSExport]
    public static void RequestRedraw(string containerId, JSObject state)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(containerId, out var resources) || resources.Context == null)
        {
            return;
        }

        try
        {
            int width = state.GetPropertyAsInt32("panelWidth");
            int height = state.GetPropertyAsInt32("panelHeight");

            float verticalOffset = (float)(double)state.GetPropertyAsDouble("verticalOffset");
            float horizontalOffset = (float)(double)state.GetPropertyAsDouble("horizontalOffset");
            float scale = (float)(double)state.GetPropertyAsDouble("scale");

            resources.Context.VerticalOffset = verticalOffset;
            resources.Context.HorizontalOffset = horizontalOffset;
            resources.Context.Scale = scale;
            resources.Context.PanelWidth = width;
            resources.Context.PanelHeight = height;

            ApplyZoomRequest(resources.Context, state);

            int forcePageSet = state.GetPropertyAsInt32("forcePageSet");
            if (forcePageSet > 0)
            {
                resources.Context.ScrollToPage(forcePageSet);
            }

            resources.Context.Search.Query = state.GetPropertyAsString("searchQuery");
            ApplyCurrentSearchResult(resources, state);

            bool pointerInside = state.GetPropertyAsBoolean("pointerInside");
            if (pointerInside)
            {
                float pointerX = (float)(double)state.GetPropertyAsDouble("pointerX");
                float pointerY = (float)(double)state.GetPropertyAsDouble("pointerY");
                resources.Context.Input.PointerPosition = new PdfPoint(pointerX, pointerY);
            }
            else
            {
                resources.Context.Input.PointerPosition = null;
            }

            bool pointerPressed = state.GetPropertyAsBoolean("pointerPressed");
            resources.Context.Input.PointerState = pointerPressed ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            resources.Context.Synchronize();

            state.SetProperty("cursorStyle", GetCursorStyle(resources.Context.Input.Cursor));
            state.SetProperty("openUri", resources.OpenUri);
            resources.OpenUri = string.Empty;

            PdfAnnotationPopup activeAnnotation = resources.Context.Annotations.ActiveAnnotation;

            if (activeAnnotation != resources.AnnotationPopup)
            {
                resources.AnnotationPopup = activeAnnotation;
                SetAnnotationPopup(state, activeAnnotation);
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
            state.SetProperty("currentPage", resources.Context.GetCurrentPage());
            state.SetProperty("pageCount", resources.Context.Pages.Count);

            resources.Context.Render();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in container '{Id}'", containerId);
        }
    }

    /// <summary>
    /// Zooms <paramref name="context"/> by the <c>zoomRequest</c> of the redraw state: to its <c>scale</c> when set,
    /// otherwise one step in or out by the sign of its <c>step</c>, around its <c>centerX</c> and <c>centerY</c>.
    /// </summary>
    private static void ApplyZoomRequest(PdfPanelContext context, JSObject state)
    {
        using JSObject zoomRequest = state.GetPropertyAsJSObject("zoomRequest");

        if (zoomRequest == null)
        {
            return;
        }

        float centerX = (float)zoomRequest.GetPropertyAsDouble("centerX");
        float centerY = (float)zoomRequest.GetPropertyAsDouble("centerY");

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

        resources.Context.Actions.UriRequested -= resources.OnUriRequested;
        resources.Context.Actions.RemoteDocumentRequested -= resources.OnRemoteDocumentRequested;
        resources.Context.Search.MatchesChanged -= resources.OnSearchMatchesChanged;
        resources.Context.Text.TextExtracted -= resources.OnTextExtracted;
        resources.Context.Dispose();
    }

    /// <summary>
    /// Sets the current search match from the <c>currentSearchResult</c> range of the redraw state,
    /// and scrolls to it when the range differs from the last one received.
    /// </summary>
    private static void ApplyCurrentSearchResult(PdfPanelResources resources, JSObject state)
    {
        PdfPanelTextRange? range = null;

        using (JSObject result = state.GetPropertyAsJSObject("currentSearchResult"))
        {
            if (result != null)
            {
                range = new PdfPanelTextRange(
                    result.GetPropertyAsInt32("pageNumber"),
                    result.GetPropertyAsInt32("startIndex"),
                    result.GetPropertyAsInt32("length"));
            }
        }

        PdfPanelSearchMatch? currentMatch = null;

        if (range != null)
        {
            foreach (PdfPanelSearchMatch match in resources.Context.Search.Matches)
            {
                if (match.Range == range.Value)
                {
                    currentMatch = match;
                    break;
                }
            }
        }

        resources.Context.Search.CurrentMatch = currentMatch;

        if (range == resources.CurrentSearchRange)
        {
            return;
        }

        resources.CurrentSearchRange = range;

        if (currentMatch != null)
        {
            resources.Context.ScrollToSearchMatch(currentMatch.Value);
        }
    }

    /// <summary>
    /// Sets the JS array built from <paramref name="matches"/> and marks the search results as changed on the redraw state.
    /// </summary>
    private static void SetSearchResults(JSObject state, IReadOnlyList<PdfPanelSearchMatch> matches)
    {
        state.SetProperty("searchResultsChanged", true);

        using JSObject results = CreateSearchResults();

        foreach (PdfPanelSearchMatch match in matches)
        {
            PdfRectangle bounds = match.Bounds;
            double[] boundsValues = [bounds.Left, bounds.Top, bounds.Right, bounds.Bottom];

            AddSearchResult(results, match.Range.PageNumber, match.Range.StartIndex, match.Range.Length, boundsValues);
        }

        state.SetProperty("searchResults", results);
    }

    /// <summary>
    /// Reads the <c>settings</c> object of the JS configuration into <paramref name="panelConfiguration"/>.
    /// Groups and properties absent from it keep their current values.
    /// </summary>
    private static void ReadConfiguration(JSObject configuration, PdfPanelConfiguration panelConfiguration)
    {
        using JSObject settingsObject = configuration.GetPropertyAsJSObject("settings");

        if (settingsObject == null)
        {
            return;
        }

        panelConfiguration.MinScale = ReadFloat(settingsObject, "minScale", panelConfiguration.MinScale);
        panelConfiguration.MaxScale = ReadFloat(settingsObject, "maxScale", panelConfiguration.MaxScale);
        panelConfiguration.ZoomStep = ReadFloat(settingsObject, "zoomStep", panelConfiguration.ZoomStep);

        using (JSObject renderer = settingsObject.GetPropertyAsJSObject("renderer"))
        {
            if (renderer != null)
            {
                panelConfiguration.BackgroundColor = ReadColor(renderer, "backgroundColor", panelConfiguration.BackgroundColor);
                panelConfiguration.PageCornerRadius = ReadFloat(renderer, "pageCornerRadius", panelConfiguration.PageCornerRadius);
                panelConfiguration.ShowPageLoadingAnimation = ReadBoolean(renderer, "showPageLoadingAnimation", panelConfiguration.ShowPageLoadingAnimation);
            }
        }

        using (JSObject layout = settingsObject.GetPropertyAsJSObject("layout"))
        {
            if (layout != null)
            {
                panelConfiguration.PageGap = ReadFloat(layout, "pageGap", panelConfiguration.PageGap);

                using JSObject padding = layout.GetPropertyAsJSObject("padding");

                if (padding != null)
                {
                    panelConfiguration.PaddingLeft = ReadFloat(padding, "left", panelConfiguration.PaddingLeft);
                    panelConfiguration.PaddingTop = ReadFloat(padding, "top", panelConfiguration.PaddingTop);
                    panelConfiguration.PaddingRight = ReadFloat(padding, "right", panelConfiguration.PaddingRight);
                    panelConfiguration.PaddingBottom = ReadFloat(padding, "bottom", panelConfiguration.PaddingBottom);
                }
            }
        }

        using (JSObject text = settingsObject.GetPropertyAsJSObject("text"))
        {
            if (text != null)
            {
                panelConfiguration.ExtractText = ReadBoolean(text, "extractText", panelConfiguration.ExtractText);
                panelConfiguration.SelectionColor = ReadColor(text, "selectionColor", panelConfiguration.SelectionColor);
            }
        }

        using (JSObject search = settingsObject.GetPropertyAsJSObject("search"))
        {
            if (search != null)
            {
                panelConfiguration.MatchCase = ReadBoolean(search, "matchCase", panelConfiguration.MatchCase);
                panelConfiguration.WholeWord = ReadBoolean(search, "wholeWord", panelConfiguration.WholeWord);
                panelConfiguration.MatchDiacritics = ReadBoolean(search, "matchDiacritics", panelConfiguration.MatchDiacritics);
                panelConfiguration.MatchColor = ReadColor(search, "matchColor", panelConfiguration.MatchColor);
                panelConfiguration.CurrentMatchColor = ReadColor(search, "currentMatchColor", panelConfiguration.CurrentMatchColor);
            }
        }
    }

    private static float? ReadFloat(JSObject source, string name, float? currentValue)
        => source.HasProperty(name) ? (float)source.GetPropertyAsDouble(name) : currentValue;

    private static bool? ReadBoolean(JSObject source, string name, bool? currentValue)
        => source.HasProperty(name) ? source.GetPropertyAsBoolean(name) : currentValue;

    private static PdfColor? ReadColor(JSObject source, string name, PdfColor? currentValue)
        => source.HasProperty(name) ? PdfColor.ParseHexColor(source.GetPropertyAsString(name)) : currentValue;

    private static string GetCursorStyle(PdfPanelCursor cursor)
    {
        return cursor switch
        {
            PdfPanelCursor.Hand => "pointer",
            PdfPanelCursor.IBeam => "text",
            _ => "default"
        };
    }

    /// <summary>
    /// Sets the JS popup object built from <paramref name="popup"/>, or null when no annotation is active,
    /// and marks the popup as changed on the redraw state.
    /// </summary>
    private static void SetAnnotationPopup(JSObject state, PdfAnnotationPopup popup)
    {
        state.SetProperty("annotationPopupChanged", true);

        if (popup == null)
        {
            state.SetProperty("annotationPopup", (JSObject)null);
            return;
        }

        using JSObject popupObject = CreateAnnotationPopup(popup.IsInteractive);

        foreach (PdfAnnotationMessage message in popup.Messages)
        {
            using JSObject messageObject = CreateMessageObject(message);
            AddAnnotationMessage(popupObject, messageObject);
        }

        state.SetProperty("annotationPopup", popupObject);
    }

    /// <summary>
    /// Builds the JS object of <paramref name="message"/> with its replies nested.
    /// </summary>
    private static JSObject CreateMessageObject(PdfAnnotationMessage message)
    {
        JSObject messageObject = CreateAnnotationMessage(message.Title, message.Contents, message.CreationDate?.ToString("o"));

        foreach (PdfAnnotationMessage reply in message.Replies)
        {
            using JSObject replyObject = CreateMessageObject(reply);
            AddAnnotationReply(messageObject, replyObject);
        }

        return messageObject;
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

    [JSImport("createSearchResults", "canvasInterop.js")]
    private static partial JSObject CreateSearchResults();

    [JSImport("addSearchResult", "canvasInterop.js")]
    private static partial void AddSearchResult(
        JSObject results,
        int pageNumber,
        int startIndex,
        int length,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] bounds);

    [JSImport("scheduleRedraw", "canvasInterop.js")]
    internal static partial void ScheduleRedraw(string containerId);

    [JSImport("createAnnotationPopup", "canvasInterop.js")]
    private static partial JSObject CreateAnnotationPopup([JSMarshalAs<JSType.Boolean>] bool isInteractive);

    [JSImport("createAnnotationMessage", "canvasInterop.js")]
    private static partial JSObject CreateAnnotationMessage(string title, string contents, string creationDate);

    [JSImport("addAnnotationMessage", "canvasInterop.js")]
    private static partial void AddAnnotationMessage(JSObject popup, JSObject message);

    [JSImport("addAnnotationReply", "canvasInterop.js")]
    private static partial void AddAnnotationReply(JSObject message, JSObject reply);
}