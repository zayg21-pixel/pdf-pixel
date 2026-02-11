using SkiaSharp;

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

    public SKPoint? PointerPosition { get; set; }

    public PdfPanelPointerState PointerState { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is DrawingRequest request)
        {
            return Scale == request.Scale &&
                PointerPosition == request.PointerPosition &&
                PointerState == request.PointerState &&
                Offset == request.Offset &&
                CanvasSize == request.CanvasSize &&
                RenderTarget == request.RenderTarget;
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