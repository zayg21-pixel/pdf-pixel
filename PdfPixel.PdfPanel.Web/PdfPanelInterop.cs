using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Settings;
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

        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddEmscriptenConsole());
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
                var renderer = new WebGlSkiaRenderer(LoggerFactory.CreateLogger<WebGlSkiaRenderer>(), selector);
                resources = new PdfPanelResources
                {
                    SkSurfaceFactory = renderer,
                    RenderTargetFactory = renderer
                };
            }
            else
            {
                var renderer = new CpuSkiaRenderer(LoggerFactory.CreateLogger<CpuSkiaRenderer>(), selector);
                resources = new PdfPanelResources
                {
                    SkSurfaceFactory = renderer,
                    RenderTargetFactory = renderer
                };
            }

            resources.YieldInterval = TimeSpan.FromMilliseconds(configuration.GetPropertyAsDouble("yieldInterval"));
            resources.Settings = ParseSettings(configuration);

            ResourcesMap[containerId] = resources;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error registering container with id '{ContainerId}'", containerId);
        }
    }

    /// <summary>
    /// Returns the currently selected text for the given container, or an empty string if nothing is selected.
    /// </summary>
    [JSExport]
    public static string GetSelectedText(string containerId)
    {
        if (!ResourcesMap.TryGetValue(containerId, out var resources) || resources.Renderer == null)
        {
            return string.Empty;
        }

        return resources.Renderer.TextLayer.SelectedText;
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
            resources.Renderer?.Dispose();
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

            PdfPageContentProvider contentProvider = new(
                resources.Document,
                new ImmidiateWorkQueue(LoggerFactory.CreateLogger<ImmidiateWorkQueue>()),
                new PdfYieldingObserverFactory(resources.YieldInterval));

            resources.Pages = PdfPanelPageCollection.FromContentProvider(contentProvider);

            Logger.LogInformation("PDF document parsed, pages={PageCount}", resources.Pages.Count);

            resources.Renderer?.Dispose();
            resources.Renderer = new PdfPanelRenderer(resources.SkSurfaceFactory, resources.Pages.ContentProvider, resources.Settings, SynchronizationContext.Current);
            resources.Context = new PdfPanelContext(resources.Pages, resources.Renderer, resources.RenderTargetFactory, resources.Settings);
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

            int forcePageSet = state.GetPropertyAsInt32("forcePageSet");
            if (forcePageSet > 0)
            {
                resources.Context.ScrollToPage(forcePageSet);
            }

            bool pointerInside = state.GetPropertyAsBoolean("pointerInside");
            if (pointerInside)
            {
                float pointerX = (float)(double)state.GetPropertyAsDouble("pointerX");
                float pointerY = (float)(double)state.GetPropertyAsDouble("pointerY");
                resources.Context.PointerPosition = new PdfPoint(pointerX, pointerY);
            }
            else
            {
                resources.Context.PointerPosition = null;
            }

            bool pointerPressed = state.GetPropertyAsBoolean("pointerPressed");
            resources.Context.PointerState = pointerPressed ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            resources.Context.Synchronize();

            string openUri = string.Empty;

            if (resources.Context.ClickedAnnotation != null)
            {
                HandleAnnotationClick(resources, resources.Context.ClickedAnnotation, out openUri);
                resources.Context.Synchronize();
            }

            state.SetProperty("cursorStyle", GetCursorStyle(resources.Context.Cursor));
            state.SetProperty("openUri", openUri);

            PdfAnnotationPopup activeAnnotation = resources.Context.ActiveAnnotation;

            if (activeAnnotation != resources.AnnotationPopup)
            {
                resources.AnnotationPopup = activeAnnotation;
                SetAnnotationPopup(state, activeAnnotation);
            }

            state.SetProperty("scrollWidth", resources.Context.ExtentWidth);
            state.SetProperty("scrollHeight", resources.Context.ExtentHeight);
            state.SetProperty("verticalOffset", resources.Context.VerticalOffset);
            state.SetProperty("horizontalOffset", resources.Context.HorizontalOffset);
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
    /// Builds <see cref="PdfPanelSettings"/> from the <c>settings</c> object of the JS configuration.
    /// Groups and properties absent from it keep their defaults.
    /// </summary>
    private static PdfPanelSettings ParseSettings(JSObject configuration)
    {
        PdfPanelSettings settings = new();
        using JSObject settingsObject =configuration.GetPropertyAsJSObject("settings");

        if (settingsObject == null)
        {
            return settings;
        }

        using (JSObject appearance =settingsObject.GetPropertyAsJSObject("appearance"))
        {
            if (appearance != null)
            {
                settings.Appearance.BackgroundColor = ReadColor(appearance, "backgroundColor", settings.Appearance.BackgroundColor);
                settings.Appearance.PageCornerRadius = ReadFloat(appearance, "pageCornerRadius", settings.Appearance.PageCornerRadius);
                settings.Appearance.ShowPageLoadingAnimation = ReadBoolean(appearance, "showPageLoadingAnimation", settings.Appearance.ShowPageLoadingAnimation);
                settings.Appearance.SelectionColor = ReadColor(appearance, "selectionColor", settings.Appearance.SelectionColor);
                settings.Appearance.SearchMatchColor = ReadColor(appearance, "searchMatchColor", settings.Appearance.SearchMatchColor);
                settings.Appearance.LineMergeThreshold = ReadFloat(appearance, "lineMergeThreshold", settings.Appearance.LineMergeThreshold);
            }
        }

        using (JSObject zoom =settingsObject.GetPropertyAsJSObject("zoom"))
        {
            if (zoom != null)
            {
                settings.Zoom.MinScale = ReadFloat(zoom, "minScale", settings.Zoom.MinScale);
                settings.Zoom.MaxScale = ReadFloat(zoom, "maxScale", settings.Zoom.MaxScale);
                settings.Zoom.ZoomStep = ReadFloat(zoom, "zoomStep", settings.Zoom.ZoomStep);
            }
        }

        using (JSObject layout =settingsObject.GetPropertyAsJSObject("layout"))
        {
            if (layout != null)
            {
                settings.Layout.PageGap = ReadFloat(layout, "pageGap", settings.Layout.PageGap);

                using JSObject padding =layout.GetPropertyAsJSObject("padding");

                if (padding != null)
                {
                    PdfRectangle currentPadding = settings.Layout.Padding;
                    settings.Layout.Padding = new PdfRectangle(
                        ReadFloat(padding, "left", currentPadding.Left),
                        ReadFloat(padding, "top", currentPadding.Top),
                        ReadFloat(padding, "right", currentPadding.Right),
                        ReadFloat(padding, "bottom", currentPadding.Bottom));
                }
            }
        }

        using (JSObject interaction =settingsObject.GetPropertyAsJSObject("interaction"))
        {
            if (interaction != null)
            {
                settings.Interaction.MinimumDragDistance = ReadFloat(interaction, "minimumDragDistance", settings.Interaction.MinimumDragDistance);
                settings.Interaction.CharacterHitRadius = ReadFloat(interaction, "characterHitRadius", settings.Interaction.CharacterHitRadius);
            }
        }

        using (JSObject search =settingsObject.GetPropertyAsJSObject("search"))
        {
            if (search != null)
            {
                settings.Search.MatchCase = ReadBoolean(search, "matchCase", settings.Search.MatchCase);
                settings.Search.WholeWord = ReadBoolean(search, "wholeWord", settings.Search.WholeWord);
            }
        }

        using (JSObject rendering =settingsObject.GetPropertyAsJSObject("rendering"))
        {
            if (rendering != null)
            {
                settings.Rendering.Antialias = ReadBoolean(rendering, "antialias", settings.Rendering.Antialias);
                settings.Rendering.SnapToDevicePixels = ReadBoolean(rendering, "snapToDevicePixels", settings.Rendering.SnapToDevicePixels);
                settings.Rendering.TileSize = ReadInt32(rendering, "tileSize", settings.Rendering.TileSize);
                settings.Rendering.AnimationFps = ReadInt32(rendering, "animationFps", settings.Rendering.AnimationFps);
                settings.Rendering.ScrollContentUpdateDelay = ReadMilliseconds(rendering, "scrollContentUpdateDelay", settings.Rendering.ScrollContentUpdateDelay);
                settings.Rendering.ZoomContentUpdateDelay = ReadMilliseconds(rendering, "zoomContentUpdateDelay", settings.Rendering.ZoomContentUpdateDelay);
            }
        }

        return settings;
    }

    private static float ReadFloat(JSObject source, string name, float defaultValue)
        => source.HasProperty(name) ? (float)source.GetPropertyAsDouble(name) : defaultValue;

    private static int ReadInt32(JSObject source, string name, int defaultValue)
        => source.HasProperty(name) ? source.GetPropertyAsInt32(name) : defaultValue;

    private static bool ReadBoolean(JSObject source, string name, bool defaultValue)
        => source.HasProperty(name) ? source.GetPropertyAsBoolean(name) : defaultValue;

    private static PdfColor ReadColor(JSObject source, string name, PdfColor defaultValue)
        => source.HasProperty(name) ? PdfColor.ParseHexColor(source.GetPropertyAsString(name)) : defaultValue;

    private static TimeSpan ReadMilliseconds(JSObject source, string name, TimeSpan defaultValue)
        => source.HasProperty(name) ? TimeSpan.FromMilliseconds(source.GetPropertyAsDouble(name)) : defaultValue;

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
    /// Handles an annotation click by processing the associated action.
    /// URI actions set <paramref name="openUri"/> for the JS side to open.
    /// GoTo actions scroll the context to the destination.
    /// </summary>
    private static void HandleAnnotationClick(
        PdfPanelResources resources,
        PdfAnnotationPopup popup,
        out string openUri)
    {
        openUri = string.Empty;

        if (popup.PageAnnotation?.Content is not PdfLinkAnnotation link)
        {
            return;
        }

        if (link.Action is PdfUriAction uriAction && uriAction.Uri != null)
        {
            openUri = uriAction.Uri.Value.ToString();
            return;
        }

        if (link.Action is PdfGoToAction goToAction)
        {
            PdfDestination actionDestination = goToAction.GetDestination();

            if (actionDestination != null)
            {
                resources.Context?.ScrollToDestination(actionDestination);
                return;
            }
        }

        if (link.Action is PdfGoToRemoteAction)
        {
            // TODO: handle remote file loading
            return;
        }

        PdfDestination linkDestination = link.GetDestination();

        if (linkDestination != null)
        {
            resources.Context?.ScrollToDestination(linkDestination);
        }
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

    [JSImport("createAnnotationPopup", "canvasInterop.js")]
    private static partial JSObject CreateAnnotationPopup([JSMarshalAs<JSType.Boolean>] bool isInteractive);

    [JSImport("createAnnotationMessage", "canvasInterop.js")]
    private static partial JSObject CreateAnnotationMessage(string title, string contents, string creationDate);

    [JSImport("addAnnotationMessage", "canvasInterop.js")]
    private static partial void AddAnnotationMessage(JSObject popup, JSObject message);

    [JSImport("addAnnotationReply", "canvasInterop.js")]
    private static partial void AddAnnotationReply(JSObject message, JSObject reply);
}