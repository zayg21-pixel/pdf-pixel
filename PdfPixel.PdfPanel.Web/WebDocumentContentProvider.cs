using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
public class WebDocumentContentProvider : IPdfPageContentProvider
{
    private readonly string _containerId;
    private readonly ILogger<WebDocumentContentProvider> _logger;
    private WebDocumentData _documentData;
    private PdfPageCacheEntry[] _cache;
    private PdfAnnotationPopup[][] _annotations;

    public WebDocumentContentProvider(string containerId)
    {
        _containerId = containerId;
        PdfPanelInterop.OnDataReceived += PdfPanelInterop_RequestCompleted;
        _logger = PdfPanelInterop.LoggerFactory.CreateLogger<WebDocumentContentProvider>();
    }

    public object DocumentLocker { get; } = new();

    public Action<PageUpdatedArgs>? OnPageUpdated { get; set; }

    private void PdfPanelInterop_RequestCompleted(EventData eventData)
    {
        if (eventData.CommandType != WorkerCommandType.PageContentReady || eventData.Data == null)
        {
            return;
        }

        if (eventData.ContainerId != _containerId)
        {
            return;
        }

        UpdateContentResponseHeader response = JsonSerializer.Deserialize(eventData.Header, InterfaceJsonContext.Default.UpdateContentResponseHeader);
        SKPicture picture = SKPicture.Deserialize(eventData.Data);
        int pageIndex = response.PageNumber - 1;

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
            foreach (PdfPageCacheEntry cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }

        _cache = new PdfPageCacheEntry[_documentData.PagesCount];

        for (int i = 0; i < _documentData.PagesCount; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, _documentData.PageInfo[i].ToPdfPanelPageInfo(), null);
        }

        _annotations = new PdfAnnotationPopup[_documentData.PagesCount][];

        if (_documentData.Annotations != null)
        {
            ILookup<int, WebAnnotationPopupData> byPage = _documentData.Annotations.ToLookup(a => a.PageNumber);

            for (int i = 0; i < _documentData.PagesCount; i++)
            {
                _annotations[i] = byPage[i + 1]
                    .Select(a => a.ToPdfAnnotationPopup())
                    .ToArray();
            }
        }
        else
        {
            for (int i = 0; i < _documentData.PagesCount; i++)
            {
                _annotations[i] = Array.Empty<PdfAnnotationPopup>();
            }
        }
    }

    public PdfAnnotationPopup[]? GetAnnotationPopups(int pageNumber)
    {
        if (_annotations == null)
        {
            return null;
        }

        return _annotations[pageNumber - 1];
    }

    public PdfPanelPageInfo GetPageInfo(int pageNumber)
    {
        if (_cache != null)
        {
            return _documentData.PageInfo[pageNumber - 1].ToPdfPanelPageInfo();
        }

        return default;
    }

    public int GetPagesCount() => _documentData?.PagesCount ?? 0;

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

        HashSet<int> pageSet = new(request.VisiblePages);

        foreach (PdfPageCacheEntry cachedPage in _cache)
        {
            if (!pageSet.Contains(cachedPage.PageNumber))
            {
                cachedPage.Cancel();
                cachedPage.Clear();
            }
        }

        WorkerInterface.UpdateContentRequest workerRequest = new()
        {
            VisiblePages = request.VisiblePages.ToList(),
            Scale = request.RenderingParameters.ScaleFactor ?? 1
        };

        string requestId = Guid.NewGuid().ToString();
        EmscriptenInterop.AllocRequestSab(_containerId, requestId);

        string requestJson = JsonSerializer.Serialize(workerRequest, InterfaceJsonContext.Default.UpdateContentRequest);
        PdfPanelInterop.SendToWorker(_containerId, requestId, WorkerCommandType.UpdateContent.ToString(), requestJson, null);
    }

    public void Dispose()
    {
        PdfPanelInterop.OnDataReceived -= PdfPanelInterop_RequestCompleted;

        if (_cache != null)
        {
            foreach (PdfPageCacheEntry cacheItem in _cache)
            {
                cacheItem.Dispose();
            }
        }
    }
}
