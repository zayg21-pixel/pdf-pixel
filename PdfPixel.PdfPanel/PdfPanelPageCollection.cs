using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PdfPixel.PdfPanel;

public delegate void AfterDrawDelegate(SKCanvas canvas, PdfPanelPage[] visiblePages, float scale); // TODO: remove?

/// <summary>
/// Page collection for binding to <see cref="SkiaPdfPanel"/>.
/// </summary>
public sealed class PdfPanelPageCollection : ReadOnlyCollection<PdfPanelPage>, IDisposable
{
    private readonly ConcurrentDictionary<int, CachedSkPicture> pictureCache = new ConcurrentDictionary<int, CachedSkPicture>();
    private readonly int[] cachedRotations;
    private readonly object disposeLocker = new object();
    private bool isDisposed;

    internal PdfPanelPageCollection(PdfPanelRenderer renderer, IList<PdfPanelPage> pages)
        : base(pages)
    {
        Renderer = renderer;
        cachedRotations = new int[Count];

        for (int i = 0; i < pages.Count; i++)
        {
            cachedRotations[i] = pages[i].UserRotation;
        }
    }

    /// <summary>
    /// Delegate that is called after the pages are drawn.
    /// </summary>
    public AfterDrawDelegate OnAfterDraw { get; set; }

    internal PdfPanelRenderer Renderer { get; }

    /// <summary>
    /// Returns the page if it exists.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="page">Viewer page.</param>
    /// <returns></returns>
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
    /// Returns annotation pupup at given rotated page location if it exists on visible pages.
    /// </summary>
    /// <param name="pageNumber">Page number.</param>
    /// <param name="point">Rotated location.</param>
    /// <param name="popup">Annotation popup.</param>
    /// <returns></returns>
    public bool TryGetPopup(int pageNumber, SKPoint point, out PdfAnnotationPopup popup)
    {
        // TODO: makes no sense, sould be page invariant
        if (!TryGetPage(pageNumber, out var page))
        {
            popup = default;
            return false;
        }

        popup = default;
        return false;

        //if (!popupCache.TryGetValue(pageNumber, out var pagePopups))
        //{
        //    popup = default;
        //    return false;
        //}

        //var matrix = VisiblePageInfo.GetPageRotationMatrix(page.Info.Width, page.Info.Height, page.UserRotation + page.Info.Rotation);
        //point = matrix.Transform(point);

        //popup = pagePopups.FirstOrDefault(x => x.Rect.Contains(point));
        //return popup != null;
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

    internal IEnumerable<CachedSkPicture> UpdateCacheWithThumbnails(
        IEnumerable<int> visiblePages,
        float scale,
        int maxThumbnailSize,
        SKPoint? pointerPosition,
        PdfPanelPointerState pointerState,
        float horizontalOffset,
        float verticalOffset)
    {
        var cachedPages = pictureCache.ToArray();

        foreach (var cachedPage in cachedPages)
        {
            if (!visiblePages.Contains(cachedPage.Key) && pictureCache.TryRemove(cachedPage.Key, out var removedPicture))
            {
                removedPicture.Dispose();
            }
        }

        foreach (var page in visiblePages)
        {
            if (!pictureCache.TryGetValue(page, out CachedSkPicture cachedPicture))
            {
                bool hasAnnotations = false;
                if (TryGetPage(page, out var newPage))
                {
                    hasAnnotations = newPage.Popups.Length > 0;
                }

                var thumbnailPicture = Renderer.GetThumbnail(page, maxThumbnailSize);
                cachedPicture = new CachedSkPicture(thumbnailPicture, page, hasAnnotations)
                {
                    Scale = scale,
                    PointerPosition = null,
                    PointerState = pointerState
                };

                lock (disposeLocker)
                {
                    if (isDisposed)
                    {
                        cachedPicture.Dispose();
                        yield break;
                    }
                    else
                    {
                        pictureCache.TryAdd(page, cachedPicture);
                    }
                }
            }

            // Update parameters for both new and existing cached pictures
            bool scaleChanged = Math.Abs(cachedPicture.Scale - scale) != 0;

            PdfAnnotationBase activeAnnotation = null;
            SKPoint? pagePointerPosition = null;
            if (cachedPicture.HasAnnotations && pointerPosition.HasValue && TryGetPage(page, out var panelPage))
            {
                SKMatrix matrix = panelPage.ViewportToPageMatrix(scale, horizontalOffset, verticalOffset);
                SKPoint testPagePoint = matrix.MapPoint(pointerPosition.Value);

                if (panelPage.IsPointInPageBounds(testPagePoint))
                {
                    pagePointerPosition = testPagePoint;
                    activeAnnotation = Renderer.GetActiveAnnotation(page, pagePointerPosition.Value);
                    panelPage.ActivePopup = panelPage.Popups.FirstOrDefault(p => p.Annotation == activeAnnotation);
                }
            }

            bool annotationChanged = !Equals(cachedPicture.ActiveAnnotation, activeAnnotation);
            bool stateChangedWithinAnnotation = cachedPicture.PointerState != pointerState && activeAnnotation != null;

            cachedPicture.Scale = scale;
            cachedPicture.PointerPosition = pagePointerPosition;
            cachedPicture.PointerState = pointerState;
            cachedPicture.ActiveAnnotation = activeAnnotation;
            

            if (scaleChanged)
            {
                cachedPicture.UpdatePicture(null);
                cachedPicture.UpdateAnnotationPicture(null);
            }
            else if (annotationChanged || stateChangedWithinAnnotation)
            {
                cachedPicture.UpdateAnnotationPicture(null);
            }

            yield return cachedPicture;
        }
    }

    internal IEnumerable<CachedSkPicture> GeneratePicturesForCachedPages()
    {
        var cachedPages = pictureCache.ToArray();

        foreach (var cachedPage in cachedPages)
        {
            var cachedPicture = cachedPage.Value;

            if (cachedPicture.Picture == null)
            {
                cachedPicture.UpdatePicture(Renderer.GetPicture(cachedPage.Key, cachedPicture.Scale));
            }

            if (cachedPicture.AnnotationPicture == null)
            {
                cachedPicture.UpdateAnnotationPicture(Renderer.GetAnnotationPicture(cachedPage.Key, cachedPicture.Scale, cachedPicture.PointerPosition, cachedPicture.PointerState));
            }

            yield return cachedPicture;
        }
    }

    internal bool TryGetPictureFromCache(int pageNumber, out CachedSkPicture picture)
    {
        return pictureCache.TryGetValue(pageNumber, out picture);
    }

    internal bool CheckDocumentUpdates()
    {
        // TODO: use in canvas!
        bool documentUpdated = false;

        foreach (var page in this)
        {
            if (cachedRotations[page.PageNumber - 1] != page.UserRotation)
            {
                documentUpdated = true;
                cachedRotations[page.PageNumber - 1] = page.UserRotation;

                if (pictureCache.TryRemove(page.PageNumber, out var removedPicture))
                {
                    removedPicture.Dispose();
                }
            }
        }

        return documentUpdated;
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
