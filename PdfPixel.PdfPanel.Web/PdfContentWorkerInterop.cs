using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Web.Emscripten;
using PdfPixel.PdfPanel.Web.WorkerInterface;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web;

public sealed class WorkerDocumentData
{
    public byte[] Data { get; set; }

    public IPdfDocument Document { get; set; }

    public PdfPanelPageCollection Pages { get; set; }

    public ImmidiateWorkQueue WorkQueue { get; set; }

    public WorkerSabObserverFactory ObserverFactory { get; set; }

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

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

    [JSImport("onDataReady", "pdfContentWorker.js")]
    public static partial void OnDataReady(string containerId, string id, string commandType, string header, byte[] response);

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
    internal static void ProcessMessage(string containerId, string id, string commandType, string header, byte[] data)
    {
        var commandTypeEnum = Enum.Parse<WorkerCommandType>(commandType);

        switch (commandTypeEnum)
        {
            case WorkerCommandType.SetFont:
            {
                var request = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.SetFontRequest);

                SetFont(request.Name, data);
                OnDataReady(containerId, id, commandType, header, default);
                break;
            }
            case WorkerCommandType.SetFallbackFont:
            {
                SetFallbackFont(data);
                OnDataReady(containerId, id, commandType, header, default);
                break;
            }
            case WorkerCommandType.UpdateContent:
            {
                var request = JsonSerializer.Deserialize(header, InterfaceJsonContext.Default.UpdateContentRequest);

                if (!_documents.TryGetValue(containerId, out var document))
                {
                    Logger.LogWarning("No document found for container '{ContainerId}' when trying to update content", containerId);
                    break;
                }

                document.ObserverFactory.SetCurrentRequestId(id);

                byte[] contentData = null;

                PagesDrawingRequest updatePagesRequest = request.DrawingRequest.ToPagesDrawingRequest();

                document.Pages.ContentProvider.OnPageUpdated = (args) =>
                {
                    var contentLocker = args.UpdatedContentType == UpdatedContentType.Content
                        ? args.ContentPictures.Content
                        : args.ContentPictures.Annotations;

                    using var picture = contentLocker.GetContent();
                    contentData = picture.Content.Serialize().Span.ToArray();

                    List<WebTextCharacter> characters = args.UpdatedContentType == UpdatedContentType.Content && args.ContentPictures.ContentCharacters != null
                        ? args.ContentPictures.ContentCharacters.Select(WebTextCharacter.FromPdfCharacter).ToList()
                        : null;

                    var headerData = JsonSerializer.Serialize(new UpdateContentResponseHeader
                    {
                        PageNumber = args.PageNumber,
                        ContentType = args.UpdatedContentType,
                        IsPartialContent = args.IsPartialContent,
                        DrawingRequest = request.DrawingRequest,
                        RegionOfInterest = WebRect.FromSkRect(args.RegionOfInterest),
                        Characters = characters
                    }, InterfaceJsonContext.Default.UpdateContentResponseHeader);

                    OnDataReady(containerId, id, WorkerCommandType.PageContentReady.ToString(), headerData, contentData);
                };

                document.Pages.ContentProvider.UpdateContent(updatePagesRequest);

                document.ObserverFactory.FreeRequest(id);

                var finalHeader = JsonSerializer.Serialize(new UpdateContentResponseHeader
                {
                    IsComplete = true,
                    DrawingRequest = request.DrawingRequest
                }, InterfaceJsonContext.Default.UpdateContentResponseHeader);

                OnDataReady(containerId, id, WorkerCommandType.PageContentReady.ToString(), finalHeader, null);

                break;
            }
            case WorkerCommandType.SetDocument:
            {
                var documentInfo = SetDocument(containerId, data);
                OnDataReady(containerId, id, commandType, null, documentInfo);
                break;
            }
        }
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

    private static void SetFallbackFont(byte[] fontData)
    {
        if (!_isInitialized)
        {
            return;
        }

        FontProvider.RegisterFallback(fontData);
        Logger.LogInformation("Registered fallback font");
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
            var workQueue = new ImmidiateWorkQueue();
            var observerFactory = new WorkerSabObserverFactory();
            var contentProvider = new PdfPageContentProvider(document, workQueue, observerFactory);
            var pages = PdfPanelPageCollection.FromContentProvider(contentProvider);

            WorkerDocumentData workerDocumentData = new WorkerDocumentData
            {
                Data = documentData,
                Pages = pages,
                Document = document,
                WorkQueue = workQueue,
                ObserverFactory = observerFactory
            };

            var pageInfos = pages.Select(x => x.Info).ToList();

            var annotations = pages
                .SelectMany(page => page.Popups.Select(popup => WebAnnotationPopupData.FromPdfAnnotationPopup(popup, page.PageNumber)))
                .ToList();

            var parsedData = new WebDocumentData
            {
                PageInfo = pageInfos.Select(WebDocumentPageInfo.FromPdfPanelPageInfo).ToList(),
                PagesCount = pageInfos.Count,
                Annotations = annotations
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
