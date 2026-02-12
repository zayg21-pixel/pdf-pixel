using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using PdfPixel.PdfPanel.Layout;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public partial class PdfPanelInterop
{
    private static ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider();
    private static readonly Dictionary<string, PdfPanelResources> ResourcesMap = new();

    [JSImport("globalThis.console.log")]
    internal static partial void Log([JSMarshalAs<JSType.String>] string message);

    [JSImport("_renderRgbaToCanvas", "canvasInterop.js")]
    internal static partial void JSRenderRgbaToCanvas([JSMarshalAs<JSType.String>] string id, int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgbaBytes);

    [JSExport]
    internal static async Task Initialize()
    {
        UiInvoker.Capture();
    }

    [JSExport]
    public static async Task RegisterCanvas(string id, JSObject configuration)
    {
        if (ResourcesMap.ContainsKey(id))
        {
            return;
        }

        var resources = new PdfPanelResources
        {
            SkSurfaceFactory = new CpuSkSurfaceFactory(SKColorType.Rgba8888, SKAlphaType.Premul),
            RenderTargetFactory = new WebRenderTargetFactory(id)
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
        resources.RenderingQueue = new PdfRenderingQueue(resources.SkSurfaceFactory);

        ResourcesMap[id] = resources;
    }

    [JSExport]
    public static async Task UnregisterCanvas(string id)
    {
        if (ResourcesMap.TryGetValue(id, out var resources))
        {
            resources.RenderingQueue.Dispose();
            ResourcesMap.Remove(id);
        }
    }

    [JSExport]
    internal static async Task SetDocument(string id, byte[] documentData)
    {
        UiInvoker.Capture();
        Log($"Loading PDF document for canvas '{id}' in ThreadDemoInterop...");
        if (!ResourcesMap.TryGetValue(id, out var resources))
        {
            Log($"Canvas resources not found for id '{id}'");
            return;
        }
        var factory = new LoggerFactory();
        var reader = new PdfDocumentReader(factory, FontProvider);
        Log($"Reading PDF document... {documentData.Length}");
        var document = reader.Read(new MemoryStream(documentData), string.Empty);
        Log($"PDF document loaded with {document.Pages.Count} pages.");
        var pages = PdfPanelPageCollection.FromDocument(document);
        resources.Context = new PdfPanelContext(pages, resources.RenderingQueue, resources.RenderTargetFactory, new PdfPanelVerticalLayout());

        var panelConfiguration = resources.Configuration;
        resources.Context.BackgroundColor = panelConfiguration.BackgroundColor;
        if (panelConfiguration.MaxThumbnailSize > 0)
        {
            resources.Context.MaxThumbnailSize = panelConfiguration.MaxThumbnailSize;
        }
        if (panelConfiguration.MinimumPageGap > 0)
        {
            resources.Context.MinimumPageGap = panelConfiguration.MinimumPageGap;
        }
        resources.Context.PagesPadding = panelConfiguration.PagesPadding;

        Log($"PDF document loaded for canvas '{id}' with {pages.Count} pages.");
    }


    [JSExport]
    public static async Task UpdateView(string id, float verticalOffset, float horizontalOffset, float scale)
    {
        if (!ResourcesMap.TryGetValue(id, out var resources) || resources.Context == null)
        {
            Log($"View is not initialized for canvas '{id}'");
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
            Log($"View is not initialized for canvas '{id}'");
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

            resources.Context.Update();

            state.SetProperty("scrollWidth", resources.Context.ExtentWidth);
            state.SetProperty("scrollHeight", resources.Context.ExtentHeight);
            state.SetProperty("verticalOffset", resources.Context.VerticalOffset);
            state.SetProperty("horizontalOffset", resources.Context.HorizontalOffset);

            resources.Context.Render();
        }
        catch (Exception ex)
        {
            Log($"Error in canvas '{id}': {ex}");
        }
    }
}