using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.Snap.Rasterization;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// Shows the visible pages of a <see cref="PdfPanelContext"/> as retained visuals: one per page, drawn in page content
/// coordinates and placed on the panel by its transform, holding the paper, the thumbnail placeholder and the content
/// tiles of <see cref="PdfPanelContext.Tiles"/>, over a shadow cast by the page's outline. Scrolling changes only the
/// transforms and the outlines; a page is drawn again when its tiles change.
/// </summary>
internal sealed class WpfPanelPages
{
    private readonly Dictionary<int, WpfPageVisual> _visuals = [];
    private readonly HashSet<int> _changedPageNumbers = [];
    private PdfPanelContext? _context;
    private PdfPanelThumbnailPlaceholderMode _placeholderMode;

    /// <summary>
    /// Gets the factory of the tiles' bitmaps the context is created with.
    /// </summary>
    public IPdfPanelTileResourceFactory TileResourceFactory { get; } = new WpfTileResourceFactory();

    /// <summary>
    /// Gets the factory of the thumbnails' bitmaps the context is created with.
    /// </summary>
    public IPdfPanelThumbnailResourceFactory ThumbnailResourceFactory { get; } = new WpfThumbnailResourceFactory();

    /// <summary>
    /// Gets the visual holding the visuals of the visible pages.
    /// </summary>
    public ContainerVisual Visual { get; } = new();

    /// <summary>
    /// Shows the pages of <paramref name="context"/>, dropping everything shown for the previous context.
    /// </summary>
    public void Attach(PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        Detach();

        _context = context;
        _context.Tiles.TilesChanged += OnTilesChanged;
        _context.Thumbnails.Cleared += OnThumbnailsCleared;
        _context.Thumbnails.ThumbnailReady += OnThumbnailReady;
    }

    /// <summary>
    /// Stops showing the pages of the attached context.
    /// </summary>
    public void Detach()
    {
        if (_context != null)
        {
            _context.Tiles.TilesChanged -= OnTilesChanged;
            _context.Thumbnails.Cleared -= OnThumbnailsCleared;
            _context.Thumbnails.ThumbnailReady -= OnThumbnailReady;
            _context = null;
        }

        Visual.Children.Clear();
        _visuals.Clear();
        _changedPageNumbers.Clear();
    }

    /// <summary>
    /// Brings the page visuals to the visible pages of the attached context after its render: adds and removes visuals,
    /// places each by its page's transform from page content coordinates to host units, offset by
    /// <paramref name="snapOffset"/> to whole device pixels, and draws again the pages whose drawing changed.
    /// </summary>
    /// <param name="hostScale">Scale from host units to device pixels.</param>
    /// <param name="snapOffset">Offset, in host units, that puts the panel's origin on a whole device pixel.</param>
    /// <param name="placeholderMode">Pages whose thumbnails are drawn in place of missing tiles.</param>
    public void Update(Point hostScale, Vector snapOffset, PdfPanelThumbnailPlaceholderMode placeholderMode)
    {
        if (_context == null)
        {
            return;
        }

        _placeholderMode = placeholderMode;

        HashSet<int> visiblePageNumbers = [];

        foreach (PdfPanelFramePage framePage in _context.VisiblePages)
        {
            visiblePageNumbers.Add(framePage.PageNumber);

            if (!_visuals.TryGetValue(framePage.PageNumber, out WpfPageVisual? pageVisual))
            {
                pageVisual = new WpfPageVisual(framePage);
                _visuals.Add(framePage.PageNumber, pageVisual);
                Visual.Children.Add(pageVisual.Root);
            }

            pageVisual.FramePage = framePage;
            pageVisual.Content.Transform = CreatePageTransform(framePage.ContentToPanel, hostScale, snapOffset);
            DrawShadow(pageVisual, hostScale, snapOffset);

            if (pageVisual.DrawnScale != _context.Scale
                || pageVisual.DrawnCornerRadius != _context.Renderer.PageCornerRadius
                || pageVisual.DrawnPlaceholderMode != _placeholderMode)
            {
                _changedPageNumbers.Add(framePage.PageNumber);
            }
        }

        List<int> hiddenPageNumbers = [];

        foreach (int pageNumber in _visuals.Keys)
        {
            if (!visiblePageNumbers.Contains(pageNumber))
            {
                hiddenPageNumbers.Add(pageNumber);
            }
        }

        foreach (int pageNumber in hiddenPageNumbers)
        {
            Visual.Children.Remove(_visuals[pageNumber].Root);
            _visuals.Remove(pageNumber);
        }

        DrawChangedPages();
    }

    /// <summary>
    /// Draws the outline of the page of <paramref name="pageVisual"/> in host units into its shadow visual, whose
    /// effect casts the shadow; the page covers the outline itself.
    /// </summary>
    private void DrawShadow(WpfPageVisual pageVisual, Point hostScale, Vector snapOffset)
    {
        if (_context == null)
        {
            return;
        }

        PdfPanelFramePage framePage = pageVisual.FramePage;
        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, framePage.Size.Width, framePage.Size.Height);
        PdfRectangle panelBounds = framePage.ContentToPanel.MapRect(PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, framePage.ContentToPanel));
        Rect hostBounds = new(
            (panelBounds.Left / hostScale.X) + snapOffset.X,
            (panelBounds.Top / hostScale.Y) + snapOffset.Y,
            panelBounds.Width / hostScale.X,
            panelBounds.Height / hostScale.Y);
        double radius = GetCornerRadius(pageBounds, _context.Renderer.PageCornerRadius) * _context.Scale / hostScale.X;

        using DrawingContext drawingContext = pageVisual.Shadow.RenderOpen();
        drawingContext.DrawRoundedRectangle(Brushes.White, null, hostBounds, radius, radius);
    }

    /// <summary>
    /// Returns <paramref name="cornerRadius"/> limited to half the shorter side of <paramref name="rectangle"/>, zero
    /// when it is not positive.
    /// </summary>
    private static double GetCornerRadius(in PdfRectangle rectangle, float cornerRadius)
        => Math.Max(0, Math.Min(cornerRadius, Math.Min(rectangle.Width, rectangle.Height) / 2));

    private static MatrixTransform CreatePageTransform(in PdfMatrix contentToPanel, Point hostScale, Vector snapOffset)
    {
        Matrix matrix = new(contentToPanel.ScaleX, contentToPanel.SkewY, contentToPanel.SkewX, contentToPanel.ScaleY, contentToPanel.TranslateX, contentToPanel.TranslateY);
        matrix.Scale(1 / hostScale.X, 1 / hostScale.Y);
        matrix.Translate(snapOffset.X, snapOffset.Y);

        MatrixTransform transform = new(matrix);
        transform.Freeze();

        return transform;
    }

    private void OnTilesChanged(object? sender, PdfPanelTilesChangedEventArgs args)
    {
        _changedPageNumbers.Add(args.PageNumber);
        DrawChangedPages();
    }

    private void OnThumbnailReady(object? sender, PdfPanelThumbnailEventArgs args)
    {
        _changedPageNumbers.Add(args.PageNumber);
        DrawChangedPages();
    }

    private void OnThumbnailsCleared(object? sender, EventArgs args)
    {
        _changedPageNumbers.UnionWith(_visuals.Keys);
        DrawChangedPages();
    }

    private void DrawChangedPages()
    {
        foreach (int pageNumber in _changedPageNumbers)
        {
            if (_visuals.TryGetValue(pageNumber, out WpfPageVisual? pageVisual))
            {
                DrawPage(pageVisual);
            }
        }

        _changedPageNumbers.Clear();
    }

    /// <summary>
    /// Draws the paper, the thumbnail placeholder and the tiles of the page of <paramref name="pageVisual"/> in page
    /// content coordinates, clipped to the page.
    /// </summary>
    private void DrawPage(WpfPageVisual pageVisual)
    {
        if (_context == null)
        {
            return;
        }

        PdfPanelFramePage framePage = pageVisual.FramePage;
        float scale = _context.Scale;
        float cornerRadius = _context.Renderer.PageCornerRadius;
        PdfPanelThumbnailPlaceholderMode placeholderMode = _placeholderMode;
        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, framePage.Size.Width, framePage.Size.Height);
        PdfRectangle snappedBounds = PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, framePage.ContentToPanel);
        Rect pageRect = new(snappedBounds.Left, snappedBounds.Top, snappedBounds.Width, snappedBounds.Height);
        double radius = GetCornerRadius(snappedBounds, cornerRadius);

        RectangleGeometry clip = new(pageRect, radius, radius);
        clip.Freeze();

        using (DrawingContext drawingContext = pageVisual.Content.RenderOpen())
        {
            drawingContext.PushClip(clip);
            drawingContext.DrawRectangle(Brushes.White, null, pageRect);

            if (placeholderMode != PdfPanelThumbnailPlaceholderMode.None
                && _context.Thumbnails.Get(framePage.PageNumber) is WpfPdfPanelThumbnailResource thumbnail)
            {
                drawingContext.DrawImage(thumbnail.Bitmap, pageRect);
            }

            drawingContext.DrawDrawing(CreateTilesDrawing(_context.Tiles.GetTiles(framePage)));
            drawingContext.Pop();
        }

        pageVisual.DrawnScale = scale;
        pageVisual.DrawnCornerRadius = cornerRadius;
        pageVisual.DrawnPlaceholderMode = placeholderMode;
    }

    /// <summary>
    /// Returns the drawing of <paramref name="tiles"/> in their order over their destinations, which lie on whole
    /// pixels, so their edges are aliased.
    /// </summary>
    private static DrawingGroup CreateTilesDrawing(IReadOnlyList<PdfPanelTile> tiles)
    {
        DrawingGroup drawing = new();
        RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
        RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.Linear);

        using (DrawingContext drawingContext = drawing.Open())
        {
            foreach (PdfPanelTile tile in tiles)
            {
                var resource = (WpfTileResource)tile.Resource;
                Rect destination = new(tile.Destination.Left, tile.Destination.Top, tile.Destination.Width, tile.Destination.Height);

                drawingContext.DrawImage(resource.Bitmap, destination);
            }
        }

        return drawing;
    }
}
