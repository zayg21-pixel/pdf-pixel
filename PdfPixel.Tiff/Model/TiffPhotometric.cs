namespace PdfPixel.Tiff.Model;

/// <summary>
/// Color model of the samples in a TIFF image (tag 262).
/// </summary>
public enum TiffPhotometric
{
    /// <summary>
    /// Bi-level or grayscale where zero is white.
    /// </summary>
    MinIsWhite = 0,

    /// <summary>
    /// Bi-level or grayscale where zero is black.
    /// </summary>
    MinIsBlack = 1,

    /// <summary>
    /// Red, green and blue samples.
    /// </summary>
    Rgb = 2,

    /// <summary>
    /// Single index sample into <see cref="TiffDirectory.ColorMap"/>.
    /// </summary>
    Palette = 3,

    /// <summary>
    /// Bi-level transparency mask for another image in the file.
    /// </summary>
    TransparencyMask = 4,

    /// <summary>
    /// Ink separations, cyan, magenta, yellow and black by default.
    /// </summary>
    Separated = 5,

    /// <summary>
    /// Luminance and chrominance samples.
    /// </summary>
    YCbCr = 6,

    /// <summary>
    /// CIE L*a*b* samples.
    /// </summary>
    CieLab = 8,

    /// <summary>
    /// ICC L*a*b* samples.
    /// </summary>
    IccLab = 9,

    /// <summary>
    /// ITU L*a*b* samples.
    /// </summary>
    ItuLab = 10,

    /// <summary>
    /// Color filter array samples.
    /// </summary>
    ColorFilterArray = 32803,

    /// <summary>
    /// Pixar LogL luminance samples.
    /// </summary>
    LogL = 32844,

    /// <summary>
    /// Pixar LogLuv samples.
    /// </summary>
    LogLuv = 32845,

    /// <summary>
    /// Linear raw camera samples.
    /// </summary>
    LinearRaw = 34892
}
