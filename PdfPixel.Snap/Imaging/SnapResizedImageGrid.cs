using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// The grid a turned or skewed image is resized to in its own orientation, and how device pixels map onto it.
/// </summary>
internal readonly struct SnapResizedImageGrid
{
    public SnapResizedImageGrid(in PdfImageRowAxisMapping horizontal, in PdfImageRowAxisMapping vertical, bool readsBottomUp, in PdfMatrix deviceToResized)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        ReadsBottomUp = readsBottomUp;
        DeviceToResized = deviceToResized;
    }

    public PdfImageRowAxisMapping Horizontal { get; }

    /// <summary>
    /// Gets how resized rows map onto sample rows, counted from the bottom when <see cref="ReadsBottomUp"/>.
    /// </summary>
    public PdfImageRowAxisMapping Vertical { get; }

    /// <summary>
    /// Gets whether the resized rows run from the image's bottom row up.
    /// </summary>
    public bool ReadsBottomUp { get; }

    public PdfMatrix DeviceToResized { get; }
}
