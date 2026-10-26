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
/// Work item that extracts a page's words without rendering it and stores them in the page's cache entry.
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

    private readonly PdfPanelPage _page;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextExtractionProperties _textExtractionProperties;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<BackgroundWorkItemComplete> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given page. The work item takes ownership of <paramref name="observer"/>
    /// and invokes <paramref name="onCompleted"/> with its completion once it finishes.
    /// </summary>
    public PdfPageExtractTextWorkItem(
        PdfPanelPage page,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _textExtractionProperties = textExtractionProperties ?? throw new ArgumentNullException(nameof(textExtractionProperties));
        _optionalContentStates = optionalContentStates;
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        var result = BackgroundWorkItemResult.Error;

        try
        {
            await _observer.YieldAsync().ConfigureAwait(false);

            if (!_page.CacheEntry.Content.Words.HasContent)
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
            _onCompleted(new BackgroundWorkItemComplete(_page.PageNumber, result, _observer));
        }
    }

    private void ExtractWords()
    {
        lock (_page.PanelDocument.DocumentLocker)
        {
            IPdfPage page = _page.PdfPage;

            using PdfCommandExecutionContext executionContext = new(
                _page.PanelDocument.Document,
                new PdfCommandExecutionParameters(),
                _page.PanelDocument.DocumentLocker,
                _optionalContentStates,
                _observer);

            using SKNoDrawCanvas canvas = new((int)_page.Size.Width, (int)_page.Size.Height);
            SkCanvasCommandProcessor processor = new(canvas, executionContext, _page.PanelDocument.Document.LoggerFactory.CreateLogger<SkCanvasCommandProcessor>());
            processor.ApplyPageTransformations(page.CropBox, scale: page.UserUnit);
            page.Render(processor, TextExtractionParameters, _observer);

            PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, PdfMatrix.Identity, _textExtractionProperties);
            _page.CacheEntry.Content.Words.SetContent(words);
        }
    }
}
