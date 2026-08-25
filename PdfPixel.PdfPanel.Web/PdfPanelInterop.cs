using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Fonts.Management;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.Rendering;
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
        DocumentReader = new PdfDocumentReader(LoggerFactory, new FontProvider(new SkiaFontSubstitutor(LoggerFactory), LoggerFactory));
        Logger.LogInformation("PdfPanelInterop initialized");

        _isInitialized = true;
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
                PagesPadding = PdfRectangle.FromLocationAndSize(
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("left"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("top"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("right"),
                    (float)(double)configuration.GetPropertyAsJSObject("pagesPadding")?.GetPropertyAsDouble("bottom")
                )
            };

            var background = configuration.GetPropertyAsString("backgroundColor");
            parsed.BackgroundColor = string.IsNullOrEmpty(background) ? PdfColors.LightGray : PdfColor.ParseHexColor(background);

            resources.Configuration = parsed;

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

        return resources.Renderer.TextSelector.SelectedText;
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

            resources.Document = DocumentReader.Read(new MemoryStream(document), string.Empty);
            resources.Pages = PdfPanelPageCollection.FromDocument(resources.Document, LoggerFactory);

            Logger.LogInformation("PDF document parsed, pages={PageCount}", resources.Pages.Count);

            resources.Renderer?.Dispose();

            var panelConfiguration = resources.Configuration;

            PdfPanelRendererProperties rendererProperties = new()
            {
                SynchronizationContext = SynchronizationContext.Current,
                BackgroundColor = panelConfiguration.BackgroundColor
            };

            resources.Renderer = new PdfPanelRenderer(resources.SkSurfaceFactory, resources.Pages.ContentProvider, null, rendererProperties);

            resources.Context = new PdfPanelContext(resources.Pages, resources.Renderer, resources.RenderTargetFactory);

            resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
            resources.Context.PagesPadding = panelConfiguration.PagesPadding;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for container '{Id}'", containerId);
        }
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

            resources.Renderer.Properties.BackgroundColor = panelConfiguration.BackgroundColor;
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
                resources.Context.PointerPosition = new PdfPoint(pointerX, pointerY);
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
            bool isPointerOverText = resources.Renderer.TextSelector.IsPointerOverText;
            state.SetProperty("cursorStyle", isInteractiveAnnotation ? "pointer" : (isPointerOverText ? "text" : "default"));
            state.SetProperty("openUri", openUri);

            if (activeAnnotation != null)
            {
                // TODO: [HIGH] need to also parse type here or refactor JS to eliminate this entierly.
                CreateAnnotationPopupState(state, string.Empty, isInteractiveAnnotation);

                AddAnnotationPopupMessages(state, activeAnnotation.Messages);
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
    /// Sends each message and its replies to the JS popup state, depth first.
    /// </summary>
    private static void AddAnnotationPopupMessages(JSObject state, PdfAnnotationMessage[] messages)
    {
        foreach (PdfAnnotationMessage message in messages)
        {
            AddAnnotationPopupMessage(
                state,
                message.Title ?? string.Empty,
                message.Contents ?? string.Empty,
                message.CreationDate?.ToString("o") ?? string.Empty);

            AddAnnotationPopupMessages(state, message.Replies);
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