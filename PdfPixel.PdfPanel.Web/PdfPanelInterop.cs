using Microsoft.Extensions.Logging;
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
using WebGL.Sample;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public partial class PdfPanelInterop
{
    private static readonly InMemorySkiaFontProvider FontProvider = new();
    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = new();

    [JSExport]
    internal static async Task Initialize()
    {
        UiInvoker.Capture();
    }

    /// <summary>
    /// Registers font data for a standard PDF font identified by its <see cref="PdfStandardFontName"/> text name.
    /// Must be called before loading any PDF documents that use the font.
    /// </summary>
    [JSExport]
    public static void SetFont(string name, byte[] fontData)
    {
        if (Enum.TryParse<PdfStandardFontName>(name, ignoreCase: true, out var standardFont))
        {
            FontProvider.RegisterStandardFont(standardFont, fontData);
            Console.WriteLine($"Registered standard font '{name}'");
        }
        else
        {
            Console.Error.WriteLine($"Unknown standard font name '{name}'. Expected one of: {string.Join(", ", Enum.GetNames<PdfStandardFontName>())}");
        }
    }

    [JSExport]
    public static async Task RegisterCanvas(string containerId, JSObject configuration)
    {
        if (ResourcesMap.ContainsKey(containerId))
        {
            return;
        }

        var glContext = await CanvasGlContext.CreateAsync($"#{containerId} .pdf-panel-canvas");
        var thumbnailGlContext = await CanvasGlContext.CreateAsync($"#{containerId} .pdf-thumbnail-canvas");
        var renderer = new WebGlSkiaRenderer(glContext, thumbnailGlContext);

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

        Task renderInvoker(Action action)
        {
            return Emscripten.RunOnMainThreadAsync(() =>
            {
                Emscripten.WebGlMakeContextCurrent(glContext.WebGlContext);

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in canvas '{containerId}': {ex}");
                }
            });
        }

        Task thumbnailRenderInvoker(Action action)
        {
            return Emscripten.RunOnMainThreadAsync(() =>
            {
                Emscripten.WebGlMakeContextCurrent(thumbnailGlContext.WebGlContext);

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in canvas '{containerId}': {ex}");
                }
            });
        }

        resources.RenderingQueue = new PdfRenderingQueue(resources.SkSurfaceFactory, renderInvoker, thumbnailRenderInvoker);

        ResourcesMap[containerId] = resources;
    }

    [JSExport]
    public static async Task UnregisterCanvas(string containerId)
    {
        if (ResourcesMap.TryGetValue(containerId, out var resources))
        {
            resources.RenderingQueue.Dispose();
            ResourcesMap.Remove(containerId);
        }
    }

    [JSExport]
    internal static async Task SetDocument(string id, byte[] documentData)
    {
        Console.WriteLine($"Loading PDF document for canvas '{id}' in ThreadDemoInterop...");
        if (!ResourcesMap.TryGetValue(id, out var resources))
        {
            Console.WriteLine($"Canvas resources not found for id '{id}'");
            return;
        }
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        try
        {
            var reader = new PdfDocumentReader(factory, FontProvider);
            Console.WriteLine($"Reading PDF document... {documentData.Length}");
            var document = reader.Read(new MemoryStream(documentData), string.Empty);
            Console.WriteLine($"PDF document loaded with {document.Pages.Count} pages.");
            var pages = PdfPanelPageCollection.FromDocument(document);
            resources.Context = new PdfPanelContext(pages, resources.RenderingQueue, resources.RenderTargetFactory, new PdfPanelVerticalLayout());

            var panelConfiguration = resources.Configuration;
            resources.Context.BackgroundColor = panelConfiguration.BackgroundColor;
            resources.Context.MaxThumbnailSize = panelConfiguration.MaxThumbnailSize;
            resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
            resources.Context.PagesPadding = panelConfiguration.PagesPadding;

            Console.WriteLine($"PDF document loaded for canvas '{id}' with {pages.Count} pages.");
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"Error loading PDF document for canvas '{id}': {ex}");
        }
    }


    [JSExport]
    public static async Task UpdateView(string id, float verticalOffset, float horizontalOffset, float scale)
    {
        if (!ResourcesMap.TryGetValue(id, out var resources) || resources.Context == null)
        {
            Console.WriteLine($"View is not initialized for canvas '{id}'");
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
        if (!ResourcesMap.TryGetValue(id, out var resources) || resources.Context == null)
        {
            Console.Error.WriteLine($"View is not initialized for canvas '{id}'");
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
            Console.Error.WriteLine($"Error in canvas '{id}': {ex}");
        }
    }
}