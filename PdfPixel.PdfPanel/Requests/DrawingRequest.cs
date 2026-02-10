using SkiaSharp;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to draw on <see cref="SkiaPdfPanel"/>.
/// </summary>
internal abstract class DrawingRequest
{
    public float Scale { get; set; }

    public SKPoint Offset { get; set; }

    public SKSize CanvasSize { get; set; }

    public IPdfPanelRenderTarget RenderTarget { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is DrawingRequest request)
        {
            return Scale == request.Scale &&
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