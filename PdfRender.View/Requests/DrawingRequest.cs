using SkiaSharp;

namespace PdfRender.View.Requests;

/// <summary>
/// Request to draw on <see cref="SkiaPdfPanel"/>.
/// </summary>
public abstract class DrawingRequest
{
    public float Scale { get; set; }

    public SKPoint Offset { get; set; }

    public SKSize CanvasSize { get; set; }

    public SKPoint CanvasScale { get; set; }

    public SKPoint CanvasOffset { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is DrawingRequest request)
        {
            return Scale == request.Scale &&
                Offset == request.Offset &&
                CanvasSize == request.CanvasSize &&
                CanvasScale == request.CanvasScale &&
                CanvasOffset == request.CanvasOffset;
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