using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PdfPixel.PdfPanel.Web;

public class WorkerDocumentData
{
    public string Id { get; set; }

    public byte[] Data { get; set; }

    public PdfDocument Document { get; set; }

    public PdfPanelPageCollection Pages { get; set; }
}

[SupportedOSPlatform("browser")]
public partial class PdfContentWorkerInterop
{
    private static readonly InMemorySkiaFontProvider FontProvider = new();
    private static bool _isInitialized = false;
    private static readonly Dictionary<string, WorkerDocumentData> _documents = new();

    /// <summary>Gets the application-wide logger factory, available after <see cref="Initialize"/> has been called.</summary>
    public static ILoggerFactory LoggerFactory { get; private set; }

    private static CancellationTokenSource _current;

    /// <summary>Gets the logger for <see cref="PdfPanelInterop"/>, available after <see cref="Initialize"/> has been called.</summary>
    public static ILogger Logger { get; private set; }

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
    internal static void ProcessMessage(JSObject state)
    {
        Logger.LogInformation("Received message from main thread");

        var message = state.GetPropertyAsString("message");
        var data = state.GetPropertyAsByteArray("data");
        var parameters = state.GetPropertyAsString("parameters");

        Logger.LogInformation("Message: {Message}, Parameters: {Parameters}, Data Length: {DataLength}", message, parameters, data?.Length ?? 0);

        switch (message)
        {
            case "setFont":
            {
                SetFont(parameters, data);
                break;
            }
            case "updateContent":
            {
                string [] parts = parameters.Split(' ');
                string canvasId = parts[0];
                int pageNumber = int.Parse(parts[1]);
                float scaleFactor = float.Parse(parts[2]);

                var document = _documents[canvasId];

                _current?.Cancel();
                _current?.Dispose();
                _current = new CancellationTokenSource();

                var request = new ContentProviderRequest
                {
                    PageNumber = pageNumber,
                    RenderingParameters = new PdfRenderingParameters
                    {
                        ScaleFactor = scaleFactor
                    },
                    CancellationTokenSource = _current, // TODO: store separately and cancel by ID
                    //OnPageContentUpdated = (pageNum, content) =>
                    //{
                    //    Logger.LogInformation("Content updated for canvas '{CanvasId}', page {PageNumber}, scale {ScaleFactor}", canvasId, pageNum, scaleFactor);
                    //    using var picture = content.GetContent();
                        
                    //    if (picture.Content != null)
                    //    {
                    //        state.SetProperty("data", picture.Content.Serialize().Span.ToArray());
                    //    }
                    //}
                };

                document.Pages.ContentProvider.UpdateContent(request);
                var content = document.Pages.ContentProvider.GetExistingContent(pageNumber);
                using var picture = content.GetContent();

                if (picture.HasContent)
                {
                    state.SetProperty("data", picture.Content.Serialize().Span.ToArray());
                }

                Logger.LogInformation("data updated for canvas '{CanvasId}', page {PageNumber}, scale {ScaleFactor}", canvasId, pageNumber, scaleFactor);

                break;
            }
            case "setDocument":
            {
                var result = SetDocument(parameters, data);
                state.SetProperty("data", result);
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

    private static byte[] SetDocument(string id, byte[] documentData)
    {
        Logger.LogInformation("Loading PDF document for canvas '{Id}'", id);

        try
        {
            var reader = new PdfDocumentReader(LoggerFactory, FontProvider);
            Logger.LogInformation("Reading PDF document, size={Size} bytes", documentData.Length);
            var document = reader.Read(new MemoryStream(documentData), string.Empty);
            Logger.LogInformation("PDF document parsed, pages={PageCount}", document.Pages.Count);
            var contentProvider = new PdfPageContentProvider(document, new ImmidiateWorkQueue<PdfPageUpdateCacheWorkItem>());
            var pages = PdfPanelPageCollection.FromContentProvider(contentProvider);

            WorkerDocumentData data = new WorkerDocumentData
            {
                Data = documentData,
                Pages = pages,
                Document = document
            };

            var pageInfos = pages.Select(x => x.Info).ToList();

            var parsedData = new WebDocumentData
            {
                CanvasId = id,
                PageInfo = pageInfos.Select(WebDocumentPageInfo.FromPdfPanelPageInfo).ToList(),
                PagesCount = pageInfos.Count
            };

            _documents[id] = data;

            Logger.LogInformation("PDF document loaded for canvas '{Id}', pages={PageCount}", id, pages.Count);
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(parsedData, JsonSourceGenerationContext.Default.WebDocumentData);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading PDF document for canvas '{Id}'", id);
            return null;
        }
    }
}
