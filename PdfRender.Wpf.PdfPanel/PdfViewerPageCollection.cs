using PdfRender.Models;
using PdfRender.Wpf.PdfPanel.Drawing;
using PdfRender.Wpf.PdfPanel.Rendering;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace PdfRender.Wpf.PdfPanel
{
    public delegate void AfterDrawDelegate(SKCanvas canvas, VisiblePageInfo[] visiblePages, double scale);

    /// <summary>
    /// Page collection for binding to <see cref="SkiaPdfPanel"/>.
    /// </summary>
    public sealed class PdfViewerPageCollection : ReadOnlyCollection<PdfViewerPage>, IDisposable
    {
        private const double PictureScaleTolerance = 10e-3;
        private readonly ConcurrentDictionary<int, CachedSkPicture> pictureCache = new ConcurrentDictionary<int, CachedSkPicture>();
        private readonly ConcurrentDictionary<int, AnnotationPopup[]> popupCache = new ConcurrentDictionary<int, AnnotationPopup[]>();
        private readonly int[] cachedRotations;
        private readonly object disposeLocker = new object();
        private bool isDisposed;

        internal PdfViewerPageCollection(IPdfRenderer renderer, IList<PdfViewerPage> pages)
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

        internal IPdfRenderer Renderer { get; }

        /// <summary>
        /// Returns the page if it exists.
        /// </summary>
        /// <param name="pageNumber">Number of the page.</param>
        /// <param name="page">Viewer page.</param>
        /// <returns></returns>
        public bool TryGetPage(int pageNumber, out PdfViewerPage page)
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
        public bool TryGetPopup(int pageNumber, Point point, out AnnotationPopup popup)
        {
            if (!TryGetPage(pageNumber, out var page))
            {
                popup = default;
                return false;
            }

            if (!popupCache.TryGetValue(pageNumber, out var pagePopups))
            {
                popup = default;
                return false;
            }

            var matrix = VisiblePageInfo.GetPageRotationMatrix(page.Info.Width, page.Info.Height, page.UserRotation + page.Info.Rotation);
            point = matrix.Transform(point);

            popup = pagePopups.FirstOrDefault(x => x.Rect.Contains(point));
            return popup != null;
        }

        /// <summary>
        /// Generates <see cref="PdfViewerPageCollection"/> from PDF document.
        /// </summary>
        /// <param name="document">PDF document.</param>
        /// <returns><see cref="PdfViewerPageCollection"/>.</returns>
        public static PdfViewerPageCollection FromDocument(PdfDocument document)
        {
            var renderer = new PdfRenderer(document);
            var pages = new List<PdfViewerPage>();

            for (int i = 0; i < document.Pages.Count; i++)
            {
                var pageNumber = i + 1;
                var info = renderer.GetPageInfo(pageNumber);
                var page = new PdfViewerPage(info, pageNumber);
                pages.Add(page);
            }

            return new PdfViewerPageCollection(renderer, pages);
        }

        internal IEnumerable<CachedSkPicture> UpdateCacheAndGetPictures(IEnumerable<int> visiblePages, double scale, int maxThumbnailSize)
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
                if (pictureCache.TryGetValue(page, out var cachedPicture))
                {
                    if (cachedPicture.Picture == null || Math.Abs(cachedPicture.Scale - scale) > PictureScaleTolerance)
                    {
                        cachedPicture.UpdatePicture(Renderer.GetPicture(page, scale), scale);
                    }

                    yield return cachedPicture;
                }
                else
                {
                    if (!popupCache.ContainsKey(page))
                    {
                        var popups = Renderer.GetAnnotationPopups(page);
                        popupCache.TryAdd(page, popups);
                    }

                    var thumbnailPicture = Renderer.GetThumbnail(page, maxThumbnailSize);

                    if (thumbnailPicture == null)
                    {
                        cachedPicture = new CachedSkPicture(null, page);
                    }
                    else
                    {
                        cachedPicture = new CachedSkPicture(thumbnailPicture, page);
                    }

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

                    yield return cachedPicture;
                }
            }
        }

        internal bool TryGetPictureFromCache(int pageNumber, out CachedSkPicture picture)
        {
            return pictureCache.TryGetValue(pageNumber, out picture);
        }

        internal bool CheckDocumentUpdates()
        {
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

        internal Size GetAreaSize(double pageGap)
        {
            double width = 0;
            double height = 0;

            for (int i = 0; i < Count; i++)
            {
                var page = this[i];
                var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);
                width = Math.Max(width, rotatedSize.Width);

                height += rotatedSize.Height;

                if (i != Count - 1)
                {
                    height += pageGap;
                }
            }

            return new Size(width, height);
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
}
