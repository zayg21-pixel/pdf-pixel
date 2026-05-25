namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// ICC color space signatures (4CC codes) used in profile headers to identify both
/// the device color space and the Profile Connection Space (PCS).
/// </summary>
public enum IccColorSpace
{
    /// <summary>
    /// Unrecognized or unspecified color space.
    /// </summary>
    Unknown,

    /// <summary>
    /// Red, Green, Blue additive color space.
    /// </summary>
    Rgb,

    /// <summary>
    /// Cyan, Magenta, Yellow, Key (Black) subtractive color space.
    /// </summary>
    Cmyk,

    /// <summary>
    /// Single-channel grayscale color space.
    /// </summary>
    Gray,

    /// <summary>
    /// CIE L*a*b* perceptual color space. One of the two valid PCS values.
    /// </summary>
    Lab,

    /// <summary>
    /// CIE XYZ tristimulus color space. One of the two valid PCS values.
    /// </summary>
    Xyz,

    /// <summary>
    /// CIE L*u*v* perceptual color space.
    /// </summary>
    Luv,

    /// <summary>
    /// Y'CbCr luma and chroma color space.
    /// </summary>
    Ycbcr,

    /// <summary>
    /// CIE Yxy chromaticity color space.
    /// </summary>
    Yxy,

    /// <summary>
    /// Hue, Saturation, Value color space.
    /// </summary>
    Hsv,

    /// <summary>
    /// Hue, Lightness, Saturation color space.
    /// </summary>
    Hls,

    /// <summary>
    /// Cyan, Magenta, Yellow subtractive color space (without black).
    /// </summary>
    Cmy
}
