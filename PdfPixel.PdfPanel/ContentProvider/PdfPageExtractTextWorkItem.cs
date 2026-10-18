using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Content;
using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
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

    private readonly IPdfDocument _document;
    private readonly object _documentLocker;
    private readonly PdfTextExtractor _textExtractor;
    private readonly PdfTextExtractionProperties _textExtractionProperties;
    private readonly IReadOnlyDictionary<PdfReference, bool>? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<int> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given cache entry. The work item takes ownership of <paramref name="observer"/>
    /// and invokes <paramref name="onCompleted"/> with the page number once it finishes.
    /// </summary>
    public PdfPageExtractTextWorkItem(
        PdfPageCacheEntry cacheEntry,
        IPdfDocument document,
        object documentLocker,
        PdfTextExtractor textExtractor,
        PdfTextExtractionProperties textExtractionProperties,
        IReadOnlyDictionary<PdfReference, bool>? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<int> onCompleted)
    {
        CacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _documentLocker = documentLocker ?? throw new ArgumentNullException(nameof(documentLocker));
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
        try
        {
            await _observer.YieldAsync().ConfigureAwait(false);

            if (!CacheEntry.Content.Words.HasContent)
            {
                ExtractWords();
            }
        }
        finally
        {
            _observer.Dispose();
            _onCompleted(CacheEntry.PageNumber);
        }
    }

    private void ExtractWords()
    {
        lock (_documentLocker)
        {
            IPdfPage page = _document.Pages[CacheEntry.PageNumber - 1];

            using PdfCommandExecutionContext executionContext = new(
                _document,
                new PdfCommandExecutionParameters(),
                _documentLocker,
                _optionalContentStates,
                _observer);

            using SKNoDrawCanvas canvas = new((int)page.CropBox.Width, (int)page.CropBox.Height);
            SkCanvasCommandProcessor processor = new(canvas, executionContext, _document.LoggerFactory.CreateLogger<SkCanvasCommandProcessor>());
            processor.ApplyPageTransformations(page.CropBox);
            page.Render(processor, TextExtractionParameters, _observer);

            PdfWord[] words = _textExtractor.ExtractWords(executionContext.ContentTree.Root, PdfMatrix.Identity, _textExtractionProperties);
            CacheEntry.Content.Words.SetContent(words);
        }
    }
}
