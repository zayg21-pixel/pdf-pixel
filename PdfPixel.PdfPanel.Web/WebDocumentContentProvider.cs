using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.ContentProvider;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Web
{
    public class WebDocumentCacheEntry
    {
        public int PageNumber { get; set; }

        public SKPicture ContentData { get; set; }
    }

    [SupportedOSPlatform("browser")]
    public class WebDocumentContentProvider : IPdfPageContentProvider
    {
        private readonly string _canvasId;
        private readonly ILogger<WebDocumentContentProvider> _logger;
        private WebDocumentData _documentData;
        private Dictionary<string, ContentProviderRequest> _pendingRequests = new Dictionary<string, ContentProviderRequest>();
        private PdfPageCacheEntryItem[] _cache;

        public WebDocumentContentProvider(string canvasId)
        {
            _canvasId = canvasId;
            PdfPanelInterop.RequestCompleted += PdfPanelInterop_RequestCompleted;
            _logger = PdfPanelInterop.LoggerFactory.CreateLogger<WebDocumentContentProvider>();
        }

        private void PdfPanelInterop_RequestCompleted(string id, byte[] data)
        {
            if (_pendingRequests.TryGetValue(id, out var request))
            {
                _logger.LogInformation("there was a pending request for id {RequestId}, data is {DataLength} bytes", id, data?.Length ?? 0);
                if (data != null)
                {
                    var picture = SKPicture.Deserialize(data);
                    var cache = _cache[request.PageNumber - 1];
                    cache.UpdateContentPicture(picture, request.RenderingParameters.ScaleFactor ?? 1);
                    request.OnPageContentUpdated?.Invoke(request.PageNumber, cache.ContentPicture);
                }

                _pendingRequests.Remove(id);
            }
            _logger.LogInformation("WebDocumentContentProvider: Received data for request {RequestId}", id);
        }

        public void UpdateDocument(WebDocumentData documentData)
        {
            _documentData = documentData;

            _cache = new PdfPageCacheEntryItem[_documentData.PagesCount];

            for (int i = 0; i < _documentData.PagesCount; i++)
            {
                _cache[i] = new PdfPageCacheEntryItem();
            }
        }

        public ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber)
        {
            return null;
        }

        public ContentLocker<SKPicture> GetExistingContent(int pageNumber)
        {
            if (_cache != null)
            {
                return _cache[pageNumber - 1].ContentPicture;
            }

            return null;
        }

        public PdfPanelPageInfo GetPageInfo(int pageNumber)
        {
            if (_cache != null)
            {
                return _documentData.PageInfo[pageNumber - 1].ToPdfPanelPageInfo();
            }

            return default;
        }

        public int GetPagesCount()
        {
            return _documentData?.PagesCount ?? 0;
        }

        public void RefreshCache(IEnumerable<int> pagesToStore)
        {
            //PdfPanelInterop.SendToWorker(Guid.NewGuid().ToString(), "refreshCache", _canvasId, null);
        }

        public void UpdateContent(ContentProviderRequest request)
        {
            string id = Guid.NewGuid().ToString();
            _pendingRequests.Add(id, request);
            PdfPanelInterop.SendToWorker(id, "updateContent", $"{_canvasId} {request.PageNumber} {request.RenderingParameters.ScaleFactor ?? 1}", null);
        }

        public void Dispose()
        {
        }

        private async void MessageQueue()
        {

        }
    }

    // Define the source generation context
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(WebDocumentData))]
    internal partial class JsonSourceGenerationContext : JsonSerializerContext
    {
    }

    public class WebDocumentData
    {
        public string CanvasId { get; set; }

        public int PagesCount { get; set; }

        public List<WebDocumentPageInfo> PageInfo { get; set; }
    }

    public class WebDocumentPageInfo
    {
        public string Label { get; set; }

        /// <summary>
        /// Original page width without rotation.
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// Original page height without rotation.
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// Page rotation in degrees.
        /// </summary>
        public int Rotation { get; set;  }

        public PdfPanelPageInfo ToPdfPanelPageInfo()
        {
            return new PdfPanelPageInfo(Label, Width, Height, Rotation);
        }

        public static WebDocumentPageInfo FromPdfPanelPageInfo(PdfPanelPageInfo pageInfo)
        {
            return new WebDocumentPageInfo
            {
                Label = pageInfo.Label,
                Width = pageInfo.Width,
                Height = pageInfo.Height,
                Rotation = pageInfo.Rotation
            };
        }
    }
}
