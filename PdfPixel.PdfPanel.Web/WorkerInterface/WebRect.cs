using SkiaSharp;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// JSON-serializable rectangle transferred between the UI thread and the web worker.
/// </summary>
public struct WebRect
{
    public float Left { get; set; }

    public float Top { get; set; }

    public float Right { get; set; }

    public float Bottom { get; set; }

    /// <summary>
    /// Converts this instance to an <see cref="SKRect"/>.
    /// </summary>
    public SKRect ToSkRect() => new(Left, Top, Right, Bottom);

    /// <summary>
    /// Creates a <see cref="WebRect"/> from an <see cref="SKRect"/>.
    /// </summary>
    public static WebRect FromSkRect(SKRect rect) => new()
    {
        Left = rect.Left,
        Top = rect.Top,
        Right = rect.Right,
        Bottom = rect.Bottom
    };
}
