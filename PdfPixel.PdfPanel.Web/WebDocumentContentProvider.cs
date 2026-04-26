using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using SkiaSharp;
using System;
using System.Collections.Generic;
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
    private PdfPageCacheEntry[] _cache;

    public WebDocumentContentProvider(string containerId)
    {
        _containerId = containerId;
        PdfPanelInterop.OnDataReceived += PdfPanelInterop_RequestCompleted;
        _logger = PdfPanelInterop.LoggerFactory.CreateLogger<WebDocumentContentProvider>();
    }

    public SemaphoreSlim DocumentLocker { get; } // not used

    public Action<PageUpdatedArgs> OnPageUpdated { get; set; }

    private void PdfPanelInterop_RequestCompleted(EventData eventData)
    {
        if (eventData.CommandType != WorkerCommandType.PageContentReady || eventData.Data == null)
        {
            return;
        }

        var response = JsonSerializer.Deserialize(eventData.Header, InterfaceJsonContext.Default.UpdateContentResponseHeader);

        if (response.ContainerId != _containerId)
        {
            return;
        }

        var picture = SKPicture.Deserialize(eventData.Data);
        var pageIndex = response.PageNumber - 1;

        if (response.ContentType == UpdatedContentType.Content)
        {
            _cache[pageIndex].Content.UpdateContentPicture(picture, response.Scale);
        }
        else if (response.ContentType == UpdatedContentType.Annotations)
        {
            _cache[pageIndex].AnnotationContent.UpdateContentPicture(picture, response.Scale);
        }

        OnPageUpdated?.Invoke(new PageUpdatedArgs(response.PageNumber, GetExistingContentPictures(response.PageNumber), response.ContentType));
    }

    public void UpdateDocument(WebDocumentData documentData)
    {
        _documentData = documentData;

        if (_cache != null)
        {
            foreach (var cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }

        _cache = new PdfPageCacheEntry[_documentData.PagesCount];

        for (int i = 0; i < _documentData.PagesCount; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, _documentData.PageInfo[i].ToPdfPanelPageInfo(), default);
        }
    }

    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber)
    {
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

    public PdfContentPictures GetExistingContentPictures(int pageNumber)
    {
        return new PdfContentPictures
        {
            Content = _cache[pageNumber - 1].Content.ContentPicture,
            Annotations = _cache[pageNumber - 1].AnnotationContent.ContentPicture
        };
    }

    public void UpdateContent(ContentProvider.UpdateContentRequest request)
    {
        if (_cache == null)
        {
            _logger.LogWarning("Cache is not initialized. Cannot update content.");
            return;
        }

        var pageSet = new HashSet<int>(request.VisiblePages);

        foreach (var cachedPage in _cache)
        {
            if (!pageSet.Contains(cachedPage.PageNumber))
            {
                cachedPage.Reset();
            }
        }

        var workerRequest = new WorkerInterface.UpdateContentRequest
        {
            ContainerId = _containerId,
            VisiblePages = request.VisiblePages,
            Scale = request.RenderingParameters.ScaleFactor ?? 1,
            CancellationId = Guid.NewGuid()
        };

        var requestJson = JsonSerializer.Serialize(workerRequest, InterfaceJsonContext.Default.UpdateContentRequest);
        PdfPanelInterop.SendToWorker(Guid.NewGuid().ToString(), WorkerCommandType.UpdateContent.ToString(), requestJson, null);
    }

    public void Dispose()
    {
        PdfPanelInterop.OnDataReceived -= PdfPanelInterop_RequestCompleted;

        if (_cache != null)
        {
            foreach (var cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }
    }
}
