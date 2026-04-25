using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Web;

public sealed class WorkerDocumentData
{
    public string Id { get; set; }

    public byte[] Data { get; set; }

    public PdfDocument Document { get; set; }

    public PdfPanelPageCollection Pages { get; set; }

    public ImmidiateWorkQueue<PdfPageUpdateCacheWorkItem> WorkQueue { get; set; }

    public void Dispose()
    {
        Document.Dispose();
        Pages.Dispose();
    }
}

[SupportedOSPlatform("browser")]
public partial class PdfContentWorkerInterop
{
    private static readonly InMemorySkiaFontProvider FontProvider = new();
    private static bool _isInitialized = false;
    private static readonly Dictionary<string, WorkerDocumentData> _documents = new();
    private static readonly Dictionary<string, (CancellationTokenSource Token, Guid Id)> _cancellationTokens = new();

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

    [JSImport("onDataReady", "pdfContentWorker.js")]
    public static partial void OnDataReady(string id, string commandType, string header, byte[] response);

    [JSExport]
    internal static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddEmscriptenConsole());
        Logger = LoggerFactory.CreateLogger<PdfContentWorkerInterop>();
        Logger.LogInformation("PdfContentWorkerInterop initialized");

        _isInitialized = true;
    }

    [JSExport]
    internal static async void ProcessMessage(string id, string commandType, string header, byte[] data)
    {
        var commandTypeEnum = Enum.Parse<WorkerCommandType>(commandType);

        switch (commandTypeEnum)
        {
            case WorkerCommandType.SetFont:
            {
                var request = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.SetFontRequest);

                SetFont(request.Name, data);
                OnDataReady(id, commandType, header, default);
                break;
            }
            case WorkerCommandType.RefreshCache:
            {
                var request = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.RefreshCacheRequest);

                if (!_documents.TryGetValue(request.ContainerId, out var document))
                {
                    Logger.LogWarning("No document found for container '{ContainerId}' when trying to update cache", request.ContainerId);
                    break;
                }

                CancellationTokenSource cancellationTokenSource = await GetCancellationTokenSource(request);

                var refreshCacheRequest = new ContentProvider.RefreshCacheRequest
                {
                    VisiblePages = request.PagesToStore,
                    CancellationTokenSource = cancellationTokenSource
                };

                document.Pages.ContentProvider.RefreshCache(refreshCacheRequest);
                OnDataReady(id, commandType, header, default);
                break;
            }
            case WorkerCommandType.UpdateContent:
            {
                var request = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.UpdateContentRequest);

                if (!_documents.TryGetValue(request.ContainerId, out var document))
                {
                    Logger.LogWarning("No document found for container '{ContainerId}' when trying to update content", request.ContainerId);
                    break;
                }

                CancellationTokenSource cancellationTokenSource = await GetCancellationTokenSource(request);

                byte[] contentData = null;

                var updatePagesRequest = new ContentProvider.UpdateContentRequest
                {
                    PageNumber = request.PageNumber,
                    RenderingParameters = new PdfRenderingParameters
                    {
                        ScaleFactor = request.Scale
                    },
                    CancellationTokenSource = cancellationTokenSource,
                    OnPageUpdated = (pageNum, content) =>
                    {
                        using var picture = content.GetContent();
                        contentData = picture.Content.Serialize().Span.ToArray();
                        OnDataReady(id, commandType, header, contentData);
                    }
                };

                document.Pages.ContentProvider.UpdateContent(updatePagesRequest);

                break;
            }
            case WorkerCommandType.SetDocument:
            {
                var parameters = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.SetDocumentRequest);
                var documentInfo = SetDocument(parameters.ContainerId, data);
                OnDataReady(id, commandType, header, documentInfo);
                break;
            }
        }
    }

    private static async Task<CancellationTokenSource> GetCancellationTokenSource(ContentRequest request)
    {
        CancellationTokenSource cancellationTokenSource;

        if (_cancellationTokens.TryGetValue(request.ContainerId, out var existingToken))
        {
            if (existingToken.Id == request.CancellationId)
            {
                cancellationTokenSource = existingToken.Token;
            }
            else
            {
                try
                {
                    existingToken.Token.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                existingToken.Token.Dispose();
                cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokens[request.ContainerId] = (cancellationTokenSource, request.CancellationId);
            }
        }
        else
        {
            cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokens[request.ContainerId] = (cancellationTokenSource, request.CancellationId);
        }

        await Task.Yield();

        return cancellationTokenSource;
    }

    private static void SetResponse(JSObject state, byte[] data)
    {
        state.SetProperty("response", data);
    }

    private static void SetFont(string name, byte[] fontData)
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
    }

    private static byte[] SetDocument(string containerId, byte[] documentData)
    {
        Logger.LogInformation("Loading PDF document for container '{Id}'", containerId);

        try
        {
            var reader = new PdfDocumentReader(LoggerFactory, FontProvider);
            Logger.LogInformation("Reading PDF document, size={Size} bytes", documentData.Length);
            var document = reader.Read(new MemoryStream(documentData), string.Empty);
            Logger.LogInformation("PDF document parsed, pages={PageCount}", document.Pages.Count);
            var workQueue = new ImmidiateWorkQueue<PdfPageUpdateCacheWorkItem>();
            var contentProvider = new PdfPageContentProvider(document, workQueue);
            var pages = PdfPanelPageCollection.FromContentProvider(contentProvider);

            WorkerDocumentData workerDocumentData = new WorkerDocumentData
            {
                Data = documentData,
                Pages = pages,
                Document = document,
                WorkQueue = workQueue
            };

            var pageInfos = pages.Select(x => x.Info).ToList();

            var parsedData = new WebDocumentData
            {
                ContainerId = containerId,
                PageInfo = pageInfos.Select(WebDocumentPageInfo.FromPdfPanelPageInfo).ToList(),
                PagesCount = pageInfos.Count
            };

            if (_documents.TryGetValue(containerId, out WorkerDocumentData value))
            {
                value.Dispose();
                _documents[containerId] = null;
                Logger.LogWarning("Document for container '{Id}' already exists. It will be replaced.", containerId);
            }

            _documents[containerId] = workerDocumentData;

            Logger.LogInformation("PDF document loaded for container '{Id}', pages={PageCount}", containerId, pages.Count);
            return JsonSerializer.SerializeToUtf8Bytes(parsedData, InterfaceJsonContext.Default.WebDocumentData);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for container '{Id}'", containerId);
            return null;
        }
    }
}
