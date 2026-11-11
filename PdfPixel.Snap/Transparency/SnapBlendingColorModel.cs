namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The process color model of a blending color space (ISO 32000-2 11.3.4).
/// </summary>
internal enum SnapBlendingColorModel
{
    Gray,

    Rgb,

    /// <summary>
    /// Four subtractive components.
    /// </summary>
    Cmyk
}
