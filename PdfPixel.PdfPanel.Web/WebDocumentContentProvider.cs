using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public class WebDocumentContentProvider : IPdfPageContentProvider
{
    private readonly string _containerId;
    private readonly ILogger<WebDocumentContentProvider> _logger;
    private WebDocumentData _documentData;
    private Dictionary<string, ContentProvider.UpdateContentRequest> _pendingRequests = new Dictionary<string, ContentProvider.UpdateContentRequest>();
    private PdfPageCacheEntryItem[] _cache;
    private Dictionary<CancellationTokenSource, Guid> _cancellationIds = new Dictionary<CancellationTokenSource, Guid>();

    public WebDocumentContentProvider(string containerId)
    {
        _containerId = containerId;
        PdfPanelInterop.RequestCompleted += PdfPanelInterop_RequestCompleted;
        _logger = PdfPanelInterop.LoggerFactory.CreateLogger<WebDocumentContentProvider>();
    }

    public SemaphoreSlim DocumentLocker { get; } // not used

    private void PdfPanelInterop_RequestCompleted(string id, byte[] data)
    {
        if (_pendingRequests.TryGetValue(id, out var request))
        {
            if (data != null)
            {
                var picture = SKPicture.Deserialize(data);
                var cache = _cache[request.PageNumber - 1];
                cache.UpdateContentPicture(picture, request.RenderingParameters.ScaleFactor ?? 1);
                request.OnPageUpdated?.Invoke(request.PageNumber, cache.ContentPicture);
            }

            _pendingRequests.Remove(id);
        }
    }

    public void UpdateDocument(WebDocumentData documentData)
    {
        _pendingRequests.Clear();
        _documentData = documentData;

        if (_cache != null)
        {
            foreach (var cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }

        _cache = new PdfPageCacheEntryItem[_documentData.PagesCount];

        for (int i = 0; i < _documentData.PagesCount; i++)
        {
            _cache[i] = new PdfPageCacheEntryItem();
        }
    }

    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber)
    {
        return null;
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

    public void RefreshCache(ContentProvider.RefreshCacheRequest request)
    {
        if (_cache == null)
        {
            _logger.LogWarning("Cache is not initialized. Cannot refresh cache.");
            return;
        }

        var pagesToStoreSet = new HashSet<int>(request.VisiblePages);

        for (int i = 0; i < pagesToStoreSet.Count; i++)
        {
            var item = _cache[i];
            int pageNumber = i + 1;

            if (!pagesToStoreSet.Contains(pageNumber))
            {
                item.Clear();
            }
        }

        var refreshCacheRequest = new WorkerInterface.RefreshCacheRequest
        {
            ContainerId = _containerId,
            PagesToStore = pagesToStoreSet.ToList(),
            CancellationId = GetCancellationId(request.CancellationTokenSource)
        };

        var requestJson = JsonSerializer.Serialize(refreshCacheRequest, InterfaceJsonContext.Default.RefreshCacheRequest);

        PdfPanelInterop.SendToWorker(Guid.NewGuid().ToString(), WorkerCommandType.UpdateCache.ToString(), requestJson, null);
    }

    public void UpdateContent(ContentProvider.UpdateContentRequest request)
    {
        if (_cache == null)
        {
            _logger.LogWarning("Cache is not initialized. Cannot update content.");
            return;
        }

        var cacheItem = _cache[request.PageNumber - 1];

        var existingContent = cacheItem.ContentPicture;

        if (existingContent.HasContent && cacheItem.Scale == (request.RenderingParameters.ScaleFactor ?? 1f))
        {
            return;
        }

        string id = Guid.NewGuid().ToString();
        _pendingRequests.Add(id, request);

        Guid cancellationId = GetCancellationId(request.CancellationTokenSource);

        var reqeuest = new WorkerInterface.UpdateContentRequest
        {
            ContainerId = _containerId,
            PageNumber = request.PageNumber,
            Scale = request.RenderingParameters.ScaleFactor ?? 1,
            CancellationId = cancellationId
        };

        var requestJson = JsonSerializer.Serialize(reqeuest, InterfaceJsonContext.Default.UpdateContentRequest);
        PdfPanelInterop.SendToWorker(id, WorkerCommandType.UpdateContent.ToString(), requestJson, null);
    }

    private Guid GetCancellationId(CancellationTokenSource cancellationTokenSource)
    {
        // TODO: [HIGH] clear IDs and verify that cancellationTokenSource is disposed, also in other places where cancellation IDs are used

        if (!_cancellationIds.TryGetValue(cancellationTokenSource, out var cancellationId))
        {
            cancellationId = Guid.NewGuid();
            _cancellationIds[cancellationTokenSource] = cancellationId;
        }

        return cancellationId;
    }

    public void Dispose()
    {
        _pendingRequests.Clear();
        if (_cache != null)
        {
            foreach (var cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }
    }
}
