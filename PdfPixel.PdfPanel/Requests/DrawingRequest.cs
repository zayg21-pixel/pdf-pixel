using SkiaSharp;
using System.Linq;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to draw on PDF Panel.
/// </summary>
public abstract class DrawingRequest
{
    public float Scale { get; set; }

    public SKPoint Offset { get; set; }

    public SKSize CanvasSize { get; set; }

    public IPdfPanelRenderTarget RenderTarget { get; set; }

    public PdfAnnotationPopup ActiveAnnotation { get; set; }

    public PdfPanelPointerState ActiveAnnotationState { get; set; }

    public VisiblePageInfo[] VisiblePages { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is DrawingRequest request)
        {
            return Scale == request.Scale &&
                ActiveAnnotation == request.ActiveAnnotation &&
                ActiveAnnotationState == request.ActiveAnnotationState &&
                Offset == request.Offset &&
                CanvasSize == request.CanvasSize &&
                RenderTarget == request.RenderTarget &&
                VisiblePages.SequenceEqual(request.VisiblePages);
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}