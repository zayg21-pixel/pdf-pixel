namespace PdfPixel.Png.Model;

/// <summary>
/// Order in which the pixels of a PNG image are stored (IHDR interlace method).
/// </summary>
public enum PngInterlaceMethod
{
    /// <summary>
    /// Rows stored top to bottom.
    /// </summary>
    None = 0,

    /// <summary>
    /// Pixels stored in the seven Adam7 passes.
    /// </summary>
    Adam7 = 1
}
