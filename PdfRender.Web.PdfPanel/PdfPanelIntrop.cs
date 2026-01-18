using Microsoft.Extensions.Logging;
using PdfReader;
using PdfRender.Canvas;
using PdfRender.Fonts.Management;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PdfRender.Web.PdfPanel
{
    [SupportedOSPlatform("browser")]
    public partial class PdfPanelIntrop
    {
        private static ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider();
        private static readonly Dictionary<string, CanvasResources> CanvasResourcesMap = new();

        [JSImport("globalThis.console.log")]
        internal static partial void Log([JSMarshalAs<JSType.String>] string message);

        [JSImport("registerCanvas", "canvasInterop.js")]
        internal static partial bool JSRegisterCanvas(
            [JSMarshalAs<JSType.String>] string id,
            [JSMarshalAs<JSType.Object>] JSObject canvas);

        [JSImport("unregisterCanvas", "canvasInterop.js")]
        internal static partial bool JSUnregisterCanvas([JSMarshalAs<JSType.String>] string id);

        [JSImport("getCanvasWidth", "canvasInterop.js")]
        internal static partial int JSGetCanvasWidth([JSMarshalAs<JSType.String>] string id);

        [JSImport("getCanvasHeight", "canvasInterop.js")]
        internal static partial int JSGetCanvasHeight([JSMarshalAs<JSType.String>] string id);

        [JSImport("renderRgbaToCanvas", "canvasInterop.js")]
        internal static partial void JSRenderRgbaToCanvas([JSMarshalAs<JSType.String>] string id, int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgbaBytes);

        [JSImport("resizeCanvas", "canvasInterop.js")]
        internal static partial bool JSResizeCanvas([JSMarshalAs<JSType.String>] string id, double cssWidth, double cssHeight, double effectiveScale);

        [JSExport]
        internal static async Task Initialize()
        {
            await JSHost.ImportAsync("canvasInterop.js", "../canvasInterop.js");
            UiInvoker.Capture();
        }

        [JSExport]
        public static async Task<bool> RegisterCanvas(string id, JSObject canvas)
        {
            bool result = JSRegisterCanvas(id, canvas);
            if (result)
            {
                var resources = new CanvasResources
                {
                    SkSurfaceFactory = new CpuSkSurfaceFactory(),
                    RenderTargetFactory = new WebRenderTargetFactory(id)
                };
                resources.RenderingQueue = new PdfRenderingQueue(resources.SkSurfaceFactory);
                resources.RenderingQueue.OnLog += (e) => Log($"[CanvasRenderingQueue:{id}] {e}");
                CanvasResourcesMap[id] = resources;
            }
            return result;
        }

        [JSExport]
        public static async Task<bool> UnregisterCanvas(string id)
        {
            bool result = JSUnregisterCanvas(id);
            if (result)
            {
                CanvasResourcesMap.Remove(id);
            }
            return result;
        }


        [JSExport]
        internal static async Task SetDocument(string id, byte[] documentData)
        {
            UiInvoker.Capture();
            Log($"Loading PDF document for canvas '{id}' in ThreadDemoInterop...");
            if (!CanvasResourcesMap.TryGetValue(id, out var resources))
            {
                Log($"Canvas resources not found for id '{id}'");
                return;
            }
            var factory = new LoggerFactory();
            var reader = new PdfDocumentReader(factory, FontProvider);
            Log($"Reading PDF document... {documentData.Length}");
            var document = reader.Read(new MemoryStream(documentData), string.Empty);
            Log($"PDF document loaded with {document.Pages.Count} pages.");
            var pages = PdfViewerPageCollection.FromDocument(document);
            resources.ViewerCanvas = new PdfViewerCanvas(pages, resources.RenderingQueue, resources.RenderTargetFactory);

            Log($"PDF document loaded for canvas '{id}' with {pages.Count} pages.");
        }

        [JSExport]
        public static async Task DrawOnCanvas(string id)
        {
            int width = JSGetCanvasWidth(id);
            int height = JSGetCanvasHeight(id);

            if (!CanvasResourcesMap.TryGetValue(id, out var resources) || resources.ViewerCanvas == null)
            {
                Log($"View is not initialized for canvas '{id}'");
                return;
            }

            try
            {
                resources.ViewerCanvas.Width = width;
                resources.ViewerCanvas.Height = height;
                resources.ViewerCanvas.Update();
                resources.ViewerCanvas.Render();
            }
            catch (Exception ex)
            {
                Log($"Error in canvas '{id}': {ex}");
            }
        }

        [JSExport]
        public static async Task<bool> ResizeCanvas(string id, double cssWidth, double cssHeight, double effectiveScale)
        {
            return JSResizeCanvas(id, cssWidth, cssHeight, effectiveScale);
        }

        [JSExport]
        public static async Task<float> GetScrollWidth(string id)
        {
            if (!CanvasResourcesMap.TryGetValue(id, out var resources) || resources.ViewerCanvas == null)
            {
                Log($"View is not initialized for canvas '{id}'");
                return 0f;
            }

            return resources.ViewerCanvas.ExtentWidth;
        }

        [JSExport]
        public static async Task<float> GetScrollHeight(string id)
        {
            if (!CanvasResourcesMap.TryGetValue(id, out var resources) || resources.ViewerCanvas == null)
            {
                Log($"View is not initialized for canvas '{id}'");
                return 0f;
            }

            return resources.ViewerCanvas.ExtentHeight;
        }

        [JSExport]
        public static async Task UpdateView(string id, float verticalOffset, float horizontalOffset, float scale)
        {
            if (!CanvasResourcesMap.TryGetValue(id, out var resources) || resources.ViewerCanvas == null)
            {
                Log($"View is not initialized for canvas '{id}'");
                return;
            }

            resources.ViewerCanvas.VerticalOffset = verticalOffset;
            resources.ViewerCanvas.HorizontalOffset = horizontalOffset;
            resources.ViewerCanvas.Scale = scale;
            resources.ViewerCanvas.Update();
        }

        [JSExport]
        public static async Task RequestRedraw(string id, JSObject state)
        {
            if (!CanvasResourcesMap.TryGetValue(id, out var resources) || resources.ViewerCanvas == null)
            {
                Log($"View is not initialized for canvas '{id}'");
                return;
            }

            try
            {
                double cssWidth = state.GetPropertyAsDouble("cssWidth");
                double cssHeight = state.GetPropertyAsDouble("cssHeight");
                double effectiveScale = state.GetPropertyAsDouble("effectiveScale");

                JSResizeCanvas(id, cssWidth, cssHeight, effectiveScale);

                float verticalOffset = (float)(double)state.GetPropertyAsDouble("verticalOffset");
                float horizontalOffset = (float)(double)state.GetPropertyAsDouble("horizontalOffset");
                float scale = (float)(double)state.GetPropertyAsDouble("scale");

                resources.ViewerCanvas.VerticalOffset = verticalOffset;
                resources.ViewerCanvas.HorizontalOffset = horizontalOffset;
                resources.ViewerCanvas.Scale = scale;
                resources.ViewerCanvas.Update();

                state.SetProperty("scrollWidth", resources.ViewerCanvas.ExtentWidth);
                state.SetProperty("scrollHeight", resources.ViewerCanvas.ExtentHeight);

                int width = JSGetCanvasWidth(id);
                int height = JSGetCanvasHeight(id);

                resources.ViewerCanvas.Width = width;
                resources.ViewerCanvas.Height = height;
                resources.ViewerCanvas.Update();
                resources.ViewerCanvas.Render();
            }
            catch (Exception ex)
            {
                Log($"Error in canvas '{id}': {ex}");
            }
        }
    }
}