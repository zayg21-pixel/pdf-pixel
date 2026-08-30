namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents color specification information from colr box or similar.
/// </summary>
public sealed class JpxColorSpecification
{
    /// <summary>
    /// Gets or sets the method (1 = enumerated, 2 = restricted ICC, 3 = any ICC, 4 = vendor).
    /// </summary>
    public byte Method { get; set; }

    /// <summary>
    /// Gets or sets the precedence (for method 3 and 4).
    /// </summary>
    public byte Precedence { get; set; }

    /// <summary>
    /// Gets or sets the color space approximation (0 = not specified, 1 = accurate).
    /// </summary>
    public byte Approximation { get; set; }

    /// <summary>
    /// Gets or sets the enumerated color space (for method 1).
    /// Common values: 16 = sRGB, 17 = Grayscale, 18 = YCC.
    /// </summary>
    public uint EnumeratedColorSpace { get; set; }

    /// <summary>
    /// Gets or sets the ICC profile data (for methods 2 and 3).
    /// </summary>
    public byte[]? IccProfile { get; set; }

    /// <summary>
    /// Gets a value indicating whether this uses enumerated color space.
    /// </summary>
    public bool IsEnumerated => Method == 1;

    /// <summary>
    /// Gets a value indicating whether this uses restricted ICC profile.
    /// </summary>
    public bool IsRestrictedIcc => Method == 2;

    /// <summary>
    /// Gets a value indicating whether this uses any ICC profile.
    /// </summary>
    public bool IsAnyIcc => Method == 3;

    /// <summary>
    /// Gets a value indicating whether this uses vendor-specific color space.
    /// </summary>
    public bool IsVendor => Method == 4;
}
