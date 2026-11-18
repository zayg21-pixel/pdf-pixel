using PdfPixel.Imaging.Processing.Pipeline;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// The host's pixels of one content tile (e.g. a bitmap), created by <see cref="IPdfPanelTileResourceFactory"/> and
/// owned by <see cref="PdfPanelTiles"/>, which writes them, reuses them for other tiles and disposes them.
/// Called on the UI thread.
/// </summary>
public interface IPdfPanelTileResource : IDisposable
{
    /// <summary>
    /// Replaces the pixels with <paramref name="samples"/>: opaque 8-bit pixels, tile size by tile size, in the channel
    /// order of <see cref="PdfPanelRenderer.TileChannelOrder"/>, valid only during the call.
    /// </summary>
    /// <param name="samples">The pixels of the tile.</param>
    void Write(PdfImageSamples samples);
}
