using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.Rendering;
using PdfPixel.PdfPanel.Web.WorkerCommands;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Web;

public class EventData
{
    public string ContainerId { get; set; }

    public Guid Id { get; set; }

    public WorkerCommandType CommandType { get; set; }

    public string Header { get; set; }

    public byte[] Data { get; set; }
}

[SupportedOSPlatform("browser")]
public partial class PdfPanelInterop
{
    private static bool _isInitialized = false;

    private static readonly InMemorySkiaFontProvider FontProvider = new();
    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = new();
    private static Dictionary<string, TaskCompletionSource<byte[]>> _pendingRequests = new Dictionary<string, TaskCompletionSource<byte[]>>();

    public static event Action<EventData> OnDataReceived;

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

    [JSExport]
    public static void ReceivedFromWorker(string containerId, string id, string commandType, string header, byte[] data)
    {
        if (_pendingRequests.TryGetValue(id, out var taskCompletionSource))
        {
            taskCompletionSource.TrySetResult(data);
            _pendingRequests.Remove(id);
        }

        OnDataReceived?.Invoke(new EventData { ContainerId = containerId, Id = Guid.Parse(id), CommandType = Enum.Parse<WorkerCommandType>(commandType), Header = header, Data = data });
    }

    [JSImport("sendToWorker", "canvasInterop.js")]
    public static partial void SendToWorker(string containerId, string id, string commandType, string header, byte[] data);

    public static Task<byte[]> SendToWorkerAsync(string containerId, Guid id, WorkerCommandType commandType, string header, byte[] data)
    {
        var tcs = new TaskCompletionSource<byte[]>();
        _pendingRequests[id.ToString()] = tcs;
        SendToWorker(containerId, id.ToString(), commandType.ToString(), header, data);
        return tcs.Task;
    }

    [JSExport]
    internal static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddEmscriptenConsole());
        Logger = LoggerFactory.CreateLogger<PdfPanelInterop>();
        Logger.LogInformation("PdfPanelInterop initialized");

        _isInitialized = true;
    }

    /// <summary>
    /// Registers font data for a standard PDF font identified by its <see cref="PdfStandardFontName"/> text name.
    /// Must be called before loading any PDF documents that use the font.
    /// </summary>
    [JSExport]
    public static async Task SetFont(string name, byte[] fontData)
    {
        if (!_isInitialized)
        {
            return;
        }
        var request = JsonSerializer.Serialize(new SetFontRequest { Name = name }, InterfaceJsonContext.Default.SetFontRequest);
        await SendToWorkerAsync(string.Empty, Guid.NewGuid(), WorkerCommandType.SetFont, request, fontData);
    }

    /// <summary>
    /// Registers font data to use as the fallback typeface when no registered font matches a requested name or glyph.
    /// Must be called before loading any PDF documents.
    /// </summary>
    [JSExport]
    public static async Task SetFallbackFont(byte[] fontData)
    {
        if (!_isInitialized)
        {
            return;
        }

        await SendToWorkerAsync(string.Empty, Guid.NewGuid(), WorkerCommandType.SetFallbackFont, string.Empty, fontData);
    }

    [JSExport]
    public static void RegisterCanvas(string containerId, JSObject configuration)
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

            // Parse configuration immediately into a strongly-typed struct
            var parsed = new PdfPanelConfiguration
            {
                MinZoom = (float)(double)configuration.GetPropertyAsDouble("minZoom"),
                MaxZoom = (float)(double)configuration.GetPropertyAsDouble("maxZoom"),
                MinimumPageGap = (float)(double)configuration.GetPropertyAsDouble("minimumPageGap"),
                PagesPadding = SKRect.Create(
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("left"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("top"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("right"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("bottom")
                )
            };

            var background = configuration.GetPropertyAsString("backgroundColor");
            if (!string.IsNullOrEmpty(background) && SKColor.TryParse(background, out var backgroundColor))
            {
                parsed.BackgroundColor = backgroundColor;
            }
            else
            {
                parsed.BackgroundColor = SKColors.LightGray;
            }

            resources.Configuration = parsed;

            var contentProvider = new WebDocumentContentProvider(containerId);

            resources.ContentProvider = contentProvider;

            resources.AnimationClock = new PdfAnimationClock();
            resources.Renderer = new PdfPanelRenderer(resources.SkSurfaceFactory, contentProvider, resources.AnimationClock, SynchronizationContext.Current);
            ResourcesMap[containerId] = resources;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error registering container with id '{ContainerId}'", containerId);
        }
    }

    [JSExport]
    public static void UnregisterCanvas(string containerId)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (ResourcesMap.TryGetValue(containerId, out var resources))
        {
            resources.AnimationClock.Dispose();
            resources.Renderer.Dispose();
            resources.ContentProvider.Dispose();
            resources.SkSurfaceFactory.Dispose();

            ResourcesMap.Remove(containerId);
        }
    }

    [JSExport]
    internal static async Task SetDocument(string containerId, byte[] document)
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

        var responseJson = await SendToWorkerAsync(containerId, Guid.NewGuid(), WorkerCommandType.SetDocument, null, document);

        var documentData = JsonSerializer.Deserialize(responseJson, InterfaceJsonContext.Default.WebDocumentData);

        resources.ContentProvider.UpdateDocument(documentData);

        var pages = PdfPanelPageCollection.FromContentProvider(resources.ContentProvider);
        resources.Context = new PdfPanelContext(pages, resources.Renderer, resources.RenderTargetFactory, new PdfPanelVerticalLayout());

        var panelConfiguration = resources.Configuration;
        resources.Context.BackgroundColor = panelConfiguration.BackgroundColor;
        resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
        resources.Context.PagesPadding = panelConfiguration.PagesPadding;
    }

    [JSExport]
    public static void RequestRedraw(string id, JSObject state)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(id, out var resources) || resources.Context == null)
        {
            return;
        }

        try
        {
            int width = state.GetPropertyAsInt32("viewportWidth");
            int height = state.GetPropertyAsInt32("viewportHeight");

            float verticalOffset = (float)(double)state.GetPropertyAsDouble("verticalOffset");
            float horizontalOffset = (float)(double)state.GetPropertyAsDouble("horizontalOffset");
            float scale = (float)(double)state.GetPropertyAsDouble("scale");

            // Sync configuration on each redraw in case it changed
            var panelConfiguration = resources.Configuration;

            resources.Context.BackgroundColor = panelConfiguration.BackgroundColor;
                resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
            resources.Context.PagesPadding = panelConfiguration.PagesPadding;

            resources.Context.VerticalOffset = verticalOffset;
            resources.Context.HorizontalOffset = horizontalOffset;
            resources.Context.Scale = scale;
            resources.Context.ViewportWidth = width;
            resources.Context.ViewportHeight = height;

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
                resources.Context.PointerPosition = new SKPoint(pointerX, pointerY);
            }
            else
            {
                resources.Context.PointerPosition = null;
            }

            bool pointerPressed = state.GetPropertyAsBoolean("pointerPressed");
            resources.Context.PointerState = pointerPressed ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            resources.Context.Update();

            // Annotation handling: detect clicks and build popup state
            var activeAnnotation = resources.Context.ActiveAnnotation;
            var activeAnnotationState = resources.Context.ActiveAnnotationState;

            string openUri = string.Empty;
            bool wasPressed = resources.LastAnnotationPopup != null
                && resources.LastAnnotationState == PdfPanelPointerState.Pressed;
            bool isPressed = activeAnnotation != null
                && activeAnnotationState == PdfPanelPointerState.Pressed;

            if (wasPressed && !isPressed)
            {
                HandleAnnotationClick(resources, resources.LastAnnotationPopup, out openUri);
                resources.Context.Update();
                activeAnnotation = resources.Context.ActiveAnnotation;
                activeAnnotationState = resources.Context.ActiveAnnotationState;
            }

            resources.LastAnnotationPopup = activeAnnotation;
            resources.LastAnnotationState = activeAnnotationState;

            bool isInteractiveAnnotation = activeAnnotation != null && activeAnnotation.IsInteractive;
            state.SetProperty("cursorStyle", isInteractiveAnnotation ? "pointer" : "default");
            state.SetProperty("openUri", openUri);

            if (activeAnnotation != null)
            {
                // TODO: [HIGH] need to also parse type here or refactor JS to eliminate this entierly.
                CreateAnnotationPopupState(state, string.Empty, isInteractiveAnnotation);

                foreach (var message in activeAnnotation.Messages)
                {
                    string dateStr = message.CreationDate?.ToString("o") ?? string.Empty;
                    AddAnnotationPopupMessage(
                        state,
                        message.Title ?? string.Empty,
                        message.Contents ?? string.Empty,
                        dateStr);
                }
            }
            else
            {
                ClearAnnotationPopupState(state);
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
            Logger.LogError(ex, "Error in container '{Id}'", id);
        }
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

        if (popup.Navigation == null)
        {
            return;
        }

        switch (popup.Navigation.NavigationType)
        {
            case PdfAnnotationNavigationType.Uri:
                openUri = popup.Navigation.Uri ?? string.Empty;
                break;
            case PdfAnnotationNavigationType.GoToDestination:
                resources.Context?.ScrollToDestination(popup.Navigation.Destination);
                break;
            case PdfAnnotationNavigationType.GoToRemote:
                // TODO: handle remote file loading
                break;
        }
    }

    [JSImport("createAnnotationPopupState", "canvasInterop.js")]
    private static partial void CreateAnnotationPopupState(
        JSObject state,
        string type,
        [JSMarshalAs<JSType.Boolean>] bool isInteractive);

    [JSImport("addAnnotationPopupMessage", "canvasInterop.js")]
    private static partial void AddAnnotationPopupMessage(
        JSObject state,
        string title,
        string content,
        string date);

    [JSImport("clearAnnotationPopupState", "canvasInterop.js")]
    private static partial void ClearAnnotationPopupState(JSObject state);
}