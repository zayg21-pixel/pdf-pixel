using PdfPixel.Color.ColorSpace;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// The samples a decoder produces at one resolution: their tile grid, reduction, depth, components and color space.
/// </summary>
public sealed class PdfImageSampleInfo
{
    /// <summary>
    /// Initializes the sample info.
    /// </summary>
    /// <param name="grid">The tile grid over the decoded samples.</param>
    /// <param name="descaleFactor">The power-of-two reduction the samples are decoded at; 1 for the stored size.</param>
    /// <param name="bitsPerComponent">The bit depth of a component.</param>
    /// <param name="componentCount">The number of color components of a sample.</param>
    /// <param name="colorSpace">The color space of the samples.</param>
    public PdfImageSampleInfo(PdfTileInfo grid, int descaleFactor, int bitsPerComponent, int componentCount, PdfColorSpaceConverter colorSpace)
    {
        Grid = grid;
        DescaleFactor = descaleFactor;
        BitsPerComponent = bitsPerComponent;
        ComponentCount = componentCount;
        ColorSpace = colorSpace;
    }

    /// <summary>
    /// Gets the tile grid over the decoded samples.
    /// </summary>
    public PdfTileInfo Grid { get; }

    /// <summary>
    /// Gets the power-of-two reduction the samples are decoded at; 1 for the stored size.
    /// </summary>
    public int DescaleFactor { get; }

    /// <summary>
    /// Gets the bit depth of a component.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Gets the number of color components of a sample.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the color space of the samples.
    /// </summary>
    public PdfColorSpaceConverter ColorSpace { get; }
}
