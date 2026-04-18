using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public partial class PdfPanelInterop
{
    private static bool _isInitialized = false;

    private static readonly InMemorySkiaFontProvider FontProvider = new();
    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = new();

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

    [JSExport]
    internal static async Task Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        UiInvoker.Capture();
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

        if (Enum.TryParse<PdfStandardFontName>(name, ignoreCase: true, out var standardFont))
        {
            FontProvider.RegisterStandardFont(standardFont, fontData);
            Logger.LogInformation("Registered standard font '{Name}'", name);
        }
        else
        {
            Logger.LogWarning("Unknown standard font name '{Name}'. Expected one of: {Names}", name, string.Join(", ", Enum.GetNames<PdfStandardFontName>()));
        }

        return;
    }

    [JSExport]
    public static async Task RegisterCanvas(string containerId, JSObject configuration)
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
            //var renderer = new WebGlSkiaRenderer(LoggerFactory.CreateLogger<WebGlSkiaRenderer>(), $"#{containerId} .pdf-panel-canvas");
            var renderer = new CpuSkiaRenderer(LoggerFactory.CreateLogger<CpuSkiaRenderer>(), $"#{containerId} .pdf-panel-canvas");

            var resources = new PdfPanelResources
            {
                SkSurfaceFactory = renderer,
                RenderTargetFactory = renderer
            };

            // Parse configuration immediately into a strongly-typed struct
            var parsed = new PdfPanelConfiguration
            {
                MinZoom = (float)(double)configuration.GetPropertyAsDouble("minZoom"),
                MaxZoom = (float)(double)configuration.GetPropertyAsDouble("maxZoom"),
                MaxThumbnailSize = configuration.GetPropertyAsInt32("maxThumbnailSize"),
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

            resources.RenderingQueue = new PdfRenderingQueue(LoggerFactory, resources.SkSurfaceFactory, new EmscriptenRenderLoopRunner());

            ResourcesMap[containerId] = resources;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error registering canvas with id '{ContainerId}'", containerId);
        }
    }

    [JSExport]
    public static async Task UnregisterCanvas(string containerId)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (ResourcesMap.TryGetValue(containerId, out var resources))
        {
            resources.RenderingQueue.Dispose();
            ResourcesMap.Remove(containerId);
        }
    }

    [JSExport]
    internal static async Task SetDocument(string id, byte[] documentData)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(id, out var resources))
        {
            return;
        }
        Logger.LogInformation("Loading PDF document for canvas '{Id}'", id);

        try
        {
            var reader = new PdfDocumentReader(LoggerFactory, FontProvider);
            Logger.LogInformation("Reading PDF document, size={Size} bytes", documentData.Length);
            var document = reader.Read(new MemoryStream(documentData), string.Empty);
            Logger.LogInformation("PDF document parsed, pages={PageCount}", document.Pages.Count);
            var pages = PdfPanelPageCollection.FromDocument(document);
            resources.Context = new PdfPanelContext(pages, resources.RenderingQueue, resources.RenderTargetFactory, new PdfPanelVerticalLayout());

            var panelConfiguration = resources.Configuration;
            resources.Context.BackgroundColor = panelConfiguration.BackgroundColor;
            resources.Context.MaxThumbnailSize = panelConfiguration.MaxThumbnailSize;
            resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
            resources.Context.PagesPadding = panelConfiguration.PagesPadding;

            Logger.LogInformation("PDF document loaded for canvas '{Id}', pages={PageCount}", id, pages.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for canvas '{Id}'", id);
        }
    }


    [JSExport]
    public static async Task UpdateView(string id, float verticalOffset, float horizontalOffset, float scale)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!ResourcesMap.TryGetValue(id, out var resources) || resources.Context == null)
        {
            return;
        }

        resources.Context.VerticalOffset = verticalOffset;
        resources.Context.HorizontalOffset = horizontalOffset;
        resources.Context.Scale = scale;
        resources.Context.Update();
    }

    [JSExport]
    public static async Task RequestRedraw(string id, JSObject state)
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
            resources.Context.MaxThumbnailSize = panelConfiguration.MaxThumbnailSize;
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
                HandleAnnotationClick(resources, resources.LastAnnotationPopup.Annotation, out openUri);
                resources.Context.Update();
                activeAnnotation = resources.Context.ActiveAnnotation;
                activeAnnotationState = resources.Context.ActiveAnnotationState;
            }

            resources.LastAnnotationPopup = activeAnnotation;
            resources.LastAnnotationState = activeAnnotationState;

            bool isInteractiveAnnotation = activeAnnotation != null && activeAnnotation.IsInteractive();
            state.SetProperty("cursorStyle", isInteractiveAnnotation ? "pointer" : "default");
            state.SetProperty("openUri", openUri);

            if (activeAnnotation != null)
            {
                string annotationType = GetAnnotationTypeName(activeAnnotation.Annotation);
                CreateAnnotationPopupState(state, annotationType, isInteractiveAnnotation);

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
            Logger.LogError(ex, "Error in canvas '{Id}'", id);
        }
    }

    /// <summary>
    /// Handles an annotation click by processing the associated action.
    /// URI actions set <paramref name="openUri"/> for the JS side to open.
    /// GoTo actions scroll the context to the destination.
    /// </summary>
    private static void HandleAnnotationClick(
        PdfPanelResources resources,
        PdfAnnotationBase annotation,
        out string openUri)
    {
        openUri = string.Empty;

        if (annotation is PdfLinkAnnotation linkAnnotation)
        {
            if (linkAnnotation.Action is PdfUriAction uriAction)
            {
                string uriString = uriAction.Uri.ToString();
                if (!string.IsNullOrEmpty(uriString))
                {
                    openUri = uriString;
                }
            }
            else if (linkAnnotation.Action is PdfGoToAction goToAction)
            {
                if (goToAction.Destination != null)
                {
                    resources.Context?.ScrollToDestination(goToAction.Destination);
                }
            }
            else if (linkAnnotation.Action is PdfGoToRemoteAction)
            {
                // TODO: complete implementation here, we need to handle request for file loading
            }
            else if (linkAnnotation.Destination != null)
            {
                resources.Context?.ScrollToDestination(linkAnnotation.Destination);
            }
        }
    }

    /// <summary>
    /// Returns a short type name for the given annotation, used in the JS popup state.
    /// </summary>
    private static string GetAnnotationTypeName(PdfAnnotationBase annotation)
    {
        return annotation switch
        {
            PdfLinkAnnotation => "link",
            PdfFileAttachmentAnnotation => "fileAttachment",
            _ => "annotation"
        };
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