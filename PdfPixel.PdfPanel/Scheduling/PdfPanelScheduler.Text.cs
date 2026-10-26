using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private readonly object _textExtractionLocker = new();
    private bool _extractText;
    private int _textExtractionStartPageNumber = 1;
    private IPdfCancellableExecutionObserver? _textExtractionObserver;

    /// <summary>
    /// Starts or stops extracting the characters of every page that has none yet, one page at a time.
    /// </summary>
    public void SetTextExtractionSettings(bool extractText)
    {
        lock (_textExtractionLocker)
        {
            _extractText = extractText;

            if (extractText)
            {
                EnqueueNextTextExtraction();
            }
            else
            {
                CancelTextExtraction();
            }
        }
    }

    /// <summary>
    /// Cancels the queued or running text extraction item.
    /// </summary>
    private void CancelTextExtraction()
    {
        lock (_textExtractionLocker)
        {
            if (_textExtractionObserver == null)
            {
                return;
            }

            _textExtractionObserver.Cancel();
            _textExtractionObserver.Dispose();
            _textExtractionObserver = null;
        }
    }

    private void EnqueueNextTextExtraction()
    {
        lock (_textExtractionLocker)
        {
            if (!_extractText || _textExtractionObserver != null)
            {
                return;
            }

            PdfPanelPage? nextPage = FindNextPageWithoutWords();

            if (nextPage == null)
            {
                return;
            }

            IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextPage.PageNumber);
            _textExtractionObserver = observer;
            _processingQueue.Enqueue(new PdfPageExtractTextWorkItem(
                nextPage,
                _textExtractor,
                _textExtractionProperties,
                _panelDocument.OptionalContentStates,
                observer,
                OnTextExtractionCompleted));
        }
    }

    private void OnTextExtractionCompleted(BackgroundWorkItemComplete complete)
    {
        if (complete.Result == BackgroundWorkItemResult.Cancelled)
        {
            return;
        }

        if (_panelDocument.Pages[complete.PageNumber - 1].CacheEntry.Content.Words.HasContent)
        {
            _panelDocument.NotifyPageTextExtracted(complete.PageNumber);
        }

        lock (_textExtractionLocker)
        {
            if (_textExtractionObserver != complete.Observer)
            {
                return;
            }

            _textExtractionObserver = null;
            EnqueueNextTextExtraction();
        }
    }

    private PdfPanelPage? FindNextPageWithoutWords()
    {
        IReadOnlyList<PdfPanelPage> pages = _panelDocument.Pages;
        int startIndex = _textExtractionStartPageNumber - 1;

        for (int offset = 0; offset < pages.Count; offset++)
        {
            PdfPanelPage page = pages[(startIndex + offset) % pages.Count];

            if (!page.CacheEntry.Content.Words.HasContent)
            {
                return page;
            }
        }

        return null;
    }
}
