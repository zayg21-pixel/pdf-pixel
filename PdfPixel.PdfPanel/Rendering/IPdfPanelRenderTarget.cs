using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Presents a rendered frame to the screen: the frame is drawn onto samples the target provides, and the target
/// presents them on its platform-specific display surface (e.g. WriteableBitmap).
/// </summary>
public interface IPdfPanelRenderTarget
{
    /// <summary>
    /// Matrix that maps host coordinates to panel pixels.
    /// </summary>
    PdfMatrix HostToPanel { get; }

    /// <summary>
    /// Returns the samples the frame is drawn onto, <paramref name="width"/> by <paramref name="height"/> pixels: the
    /// red, green and blue components in any order, with premultiplied alpha or none. The same samples are returned
    /// while the size stays the same, and they keep the frame for the next render pass.
    /// Called on the UI thread before each render pass.
    /// </summary>
    /// <param name="width">The width of the frame in pixels.</param>
    /// <param name="height">The height of the frame in pixels.</param>
    PdfImageSamples GetFrameSamples(int width, int height);

    /// <summary>
    /// Presents the frame drawn onto the samples of <see cref="GetFrameSamples"/>, laid out as <paramref name="frame"/>.
    /// Called on the UI thread after each render pass.
    /// </summary>
    /// <param name="frame">The size, host mapping and visible pages of the presented frame.</param>
    void Present(PdfPanelFrame frame);
}
