using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.TextExtraction;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that extracts a page's characters without rendering it and stores them in <see cref="CacheEntry"/>.
/// </summary>
public sealed class PdfPageExtractTextWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters TextExtractionParameters = new()
    {
        RenderPaths = false,
        RenderImages = false,
        RenderShadings = false,
        RenderText = false,
        ExtractText = true
    };

    private readonly IPdfDocument _document;
    private readonly object _documentLocker;
    private readonly PdfTextBlockFlattener _textBlockFlattener;
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
        PdfTextBlockFlattener textBlockFlattener,
        IPdfCancellableExecutionObserver observer,
        Action<int> onCompleted)
    {
        CacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _documentLocker = documentLocker ?? throw new ArgumentNullException(nameof(documentLocker));
        _textBlockFlattener = textBlockFlattener ?? throw new ArgumentNullException(nameof(textBlockFlattener));
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

            if (CacheEntry.Content.Characters == null)
            {
                ExtractCharacters();
            }
        }
        finally
        {
            _observer.Dispose();
            _onCompleted(CacheEntry.PageNumber);
        }
    }

    private void ExtractCharacters()
    {
        lock (_documentLocker)
        {
            IPdfPage page = _document.Pages[CacheEntry.PageNumber - 1];

            using PdfCommandExecutionContext executionContext = new(
                _document,
                new PdfCommandExecutionParameters(),
                _documentLocker,
                _document.OptionalContentGroups,
                _observer);

            PdfTextExtractionCommandProcessor processor = new(executionContext);
            processor.ApplyPageTransformations(page.CropBox);
            page.Render(processor, TextExtractionParameters, _observer);

            PdfCharacter[] characters = _textBlockFlattener.Flatten(executionContext.GetRootTextBlock(), PdfMatrix.Identity);
            CacheEntry.Content.UpdateCharacters(characters);
        }
    }
}
