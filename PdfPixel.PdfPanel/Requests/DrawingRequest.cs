using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Base class for all rendering requests sent to <see cref="PdfPanelRenderer"/>.
/// </summary>
public abstract class DrawingRequest
{
    /// <summary>
    /// Current zoom scale factor.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// Horizontal and vertical scroll offset in scaled canvas space.
    /// </summary>
    public PdfPoint Offset { get; set; }

    /// <summary>
    /// Width and height of the drawing surface in device pixels.
    /// </summary>
    public PdfSize CanvasSize { get; set; }

    /// <summary>
    /// Target that receives the rendered surface for display.
    /// </summary>
    public IPdfPanelRenderTarget? RenderTarget { get; set; }

    /// <summary>
    /// The annotation currently under the pointer, or <see langword="null"/> if none.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; set; }

    /// <summary>
    /// Interaction state of <see cref="ActiveAnnotation"/>.
    /// </summary>
    public PdfPanelPointerState ActiveAnnotationState { get; set; }

    /// <summary>
    /// Pages visible in the current viewport, in render order.
    /// </summary>
    public VisiblePageInfo[] VisiblePages { get; set; } = [];

    /// <summary>
    /// Returns the visible page with the given page number.
    /// </summary>
    public VisiblePageInfo GetPage(int pageNumber) => VisiblePages.First(page => page.PageNumber == pageNumber);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is DrawingRequest request)
        {
            return Scale == request.Scale
                && ActiveAnnotation == request.ActiveAnnotation
                && ActiveAnnotationState == request.ActiveAnnotationState
                && Offset == request.Offset
                && CanvasSize == request.CanvasSize
                && RenderTarget == request.RenderTarget
                && VisiblePages.SequenceEqual(request.VisiblePages);
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Scale, Offset, CanvasSize, RenderTarget, ActiveAnnotation, ActiveAnnotationState, VisiblePages);
}
