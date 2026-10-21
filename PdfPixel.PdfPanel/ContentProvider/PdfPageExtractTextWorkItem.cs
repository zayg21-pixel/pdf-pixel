using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that extracts a page's words without rendering it and stores them in <see cref="CacheEntry"/>.
/// </summary>
internal sealed class PdfPageExtractTextWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters TextExtractionParameters = new()
    {
        ProcessPaths = false,
        ProcessImages = false,
        ProcessShadings = false,
        ProcessText = true,
        RenderContent = false,
        ExtractContent = true
    };

    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextExtractionProperties _textExtractionProperties;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<BackgroundWorkItemComplete> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given cache entry. The work item takes ownership of <paramref name="observer"/>
    /// and invokes <paramref name="onCompleted"/> with its completion once it finishes.
    /// </summary>
    public PdfPageExtractTextWorkItem(
        PdfPageCacheEntry cacheEntry,
        PdfPanelDocument panelDocument,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        CacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _textExtractionProperties = textExtractionProperties ?? throw new ArgumentNullException(nameof(textExtractionProperties));
        _optionalContentStates = optionalContentStates;
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <summary>
    /// The cache entry this work item will populate.
    /// </summary>
    public PdfPageCacheEntry CacheEntry { get; }

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        var result = BackgroundWorkItemResult.Error;

        try
        {
            await _observer.YieldAsync().ConfigureAwait(false);

            if (!CacheEntry.Content.Words.HasContent)
            {
                ExtractWords();
            }

            result = BackgroundWorkItemResult.Success;
        }
        catch (OperationCanceledException)
        {
            result = BackgroundWorkItemResult.Cancelled;
            throw;
        }
        finally
        {
            _observer.Dispose();
            _onCompleted(new BackgroundWorkItemComplete(CacheEntry.PageNumber, result, _observer));
        }
    }

    private void ExtractWords()
    {
        lock (_panelDocument.DocumentLocker)
        {
            IPdfPage page = _panelDocument.Document.Pages[CacheEntry.PageNumber - 1];

            using PdfCommandExecutionContext executionContext = new(
                _panelDocument.Document,
                new PdfCommandExecutionParameters(),
                _panelDocument.DocumentLocker,
                _optionalContentStates,
                _observer);

            using SKNoDrawCanvas canvas = new((int)page.CropBox.Width, (int)page.CropBox.Height);
            SkCanvasCommandProcessor processor = new(canvas, executionContext, _panelDocument.Document.LoggerFactory.CreateLogger<SkCanvasCommandProcessor>());
            processor.ApplyPageTransformations(page.CropBox);
            page.Render(processor, TextExtractionParameters, _observer);

            PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, PdfMatrix.Identity, _textExtractionProperties);
            CacheEntry.Content.Words.SetContent(words);
        }
    }
}
