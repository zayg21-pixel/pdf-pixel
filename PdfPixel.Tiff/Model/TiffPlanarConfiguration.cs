namespace PdfPixel.Tiff.Model;

/// <summary>
/// Arrangement of the samples of a pixel in a TIFF image (tag 284).
/// </summary>
public enum TiffPlanarConfiguration
{
    /// <summary>
    /// Planar configuration not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The samples of each pixel are stored together.
    /// </summary>
    Chunky = 1,

    /// <summary>
    /// Each sample is stored in its own plane of strips or tiles.
    /// </summary>
    Planar = 2
}
