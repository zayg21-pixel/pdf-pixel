using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Page collection for binding to <see cref="PdfPanelContext"/>.
/// </summary>
public sealed class PdfPanelPageCollection : ReadOnlyCollection<PdfPanelPage>, IDisposable
{
    private readonly ConcurrentDictionary<int, CachedSkPicture> pictureCache = new ConcurrentDictionary<int, CachedSkPicture>();
    private readonly object disposeLocker = new object();
    private bool isDisposed;

    internal PdfPanelPageCollection(PdfPanelRenderer renderer, IList<PdfPanelPage> pages)
        : base(pages)
    {
        Renderer = renderer;
    }

    internal PdfPanelRenderer Renderer { get; }

    /// <summary>
    /// Returns the page if it exists.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="page">Viewer page.</param>
    /// <returns>True if page found.</returns>
    public bool TryGetPage(int pageNumber, out PdfPanelPage page)
    {
        if (pageNumber < 1 || pageNumber > Count)
        {
            page = null;
            return false;
        }

        page = this[pageNumber - 1];
        return true;
    }

    /// <summary>
    /// Gets the annotation popup at the specified page position.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="pagePosition">Position on the page in PDF coordinates.</param>
    /// <returns>The annotation popup if found; otherwise, null.</returns>
    public PdfAnnotationPopup GetAnnotationPopupAt(int pageNumber, SKPoint pagePosition)
    {
        if (!TryGetPage(pageNumber, out var page))
        {
            return null;
        }

        var activeAnnotation = Renderer.GetActiveAnnotation(pageNumber, pagePosition);
        if (activeAnnotation == null)
        {
            return null;
        }

        foreach (var popup in page.Popups)
        {
            if (popup.Annotation == activeAnnotation)
            {
                return popup;
            }
        }

        return null;
    }

    /// <summary>
    /// Generates <see cref="PdfPanelPageCollection"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <returns><see cref="PdfPanelPageCollection"/>.</returns>
    public static PdfPanelPageCollection FromDocument(PdfDocument document)
    {
        var renderer = new PdfPanelRenderer(document);
        var pages = new List<PdfPanelPage>();

        for (int i = 0; i < document.Pages.Count; i++)
        {
            var pageNumber = i + 1;
            var info = renderer.GetPageInfo(pageNumber);
            var popups = renderer.CreateAnnotationPopups(pageNumber);
            var page = new PdfPanelPage(info, pageNumber, popups);
            pages.Add(page);
        }

        return new PdfPanelPageCollection(renderer, pages);
    }

    internal void UpdateCache(IEnumerable<int> visiblePages)
    {
        var cachedPages = pictureCache.ToArray();

        foreach (var cachedPage in cachedPages)
        {
            if (!visiblePages.Contains(cachedPage.Key) && pictureCache.TryRemove(cachedPage.Key, out var removedPicture))
            {
                removedPicture.Dispose();
            }
        }
    }

    internal CachedSkPicture InitializePageWithThumbnail(
        int pageNumber,
        SKSurface thumbnailSurface,
        PdfAnnotationPopup activeAnnotationPopup,
        PdfPanelPointerState activeAnnotationState,
        CancellationToken token)
    {
        if (!pictureCache.TryGetValue(pageNumber, out CachedSkPicture cachedPicture))
        {
            bool hasAnnotations = false;
            if (TryGetPage(pageNumber, out var newPage))
            {
                hasAnnotations = newPage.Popups.Length > 0;
            }

            var recording = Renderer.GetRecording(pageNumber, token);
            SKImage thumbnailPicture = Renderer.GetThumbnail(pageNumber, recording, thumbnailSurface, token);

            cachedPicture = new CachedSkPicture(recording, thumbnailPicture, pageNumber, hasAnnotations)
            {
                ActiveAnnotationState = PdfPanelPointerState.None
            };

            lock (disposeLocker)
            {
                if (isDisposed)
                {
                    cachedPicture.Dispose();
                }
                else
                {
                    pictureCache.TryAdd(pageNumber, cachedPicture);
                }
            }
        }

        PdfAnnotationBase pageActiveAnnotation = null;
        PdfPanelPointerState pointerState = PdfPanelPointerState.None;

        if (cachedPicture.HasAnnotations && activeAnnotationPopup != null && TryGetPage(pageNumber, out var panelPage))
        {
            foreach (var popup in panelPage.Popups)
            {
                if (popup == activeAnnotationPopup)
                {
                    pageActiveAnnotation = activeAnnotationPopup.Annotation;
                    pointerState = activeAnnotationState;
                    break;
                }
            }
        }

        bool annotationChanged = cachedPicture.ActiveAnnotation != pageActiveAnnotation;
        bool stateChangedWithinAnnotation = cachedPicture.ActiveAnnotationState != pointerState && pageActiveAnnotation != null;

        cachedPicture.ActiveAnnotationState = pointerState;
        cachedPicture.ActiveAnnotation = pageActiveAnnotation;

        if (annotationChanged || stateChangedWithinAnnotation)
        {
            cachedPicture.UpdateAnnotationRecording(null);
        }

        return cachedPicture;
    }

    internal CachedSkPicture GeneratePicturesForPage(int pageNumber, float scale, CancellationToken token)
    {
        var cachedPicture = GetCachedPicture(pageNumber);

        if (cachedPicture.AnnotationRecording == null)
        {
            cachedPicture.UpdateAnnotationRecording(
                Renderer.GetAnnotationRecording(pageNumber, scale, cachedPicture.ActiveAnnotation, cachedPicture.ActiveAnnotationState, token));
        }

        return cachedPicture;
    }

    internal CachedSkPicture GetCachedPicture(int pageNumber)
    {
        if (pictureCache.TryGetValue(pageNumber, out CachedSkPicture cachedPicture))
        {
            return cachedPicture;
        }

        throw new InvalidOperationException("Cached picture not found for page " + pageNumber);
    }

    internal bool TryGetPictureFromCache(int pageNumber, out CachedSkPicture picture)
    {
        return pictureCache.TryGetValue(pageNumber, out picture);
    }

    public void Dispose()
    {
        lock (disposeLocker)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            Renderer.Dispose();

            foreach (var picture in pictureCache.Values)
            {
                picture.Dispose();
            }

            pictureCache.Clear();
        }
    }
}
