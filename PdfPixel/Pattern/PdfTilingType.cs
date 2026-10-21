namespace PdfPixel.Pattern;

/// <summary>
/// Strongly typed tiling type (PDF spec Table 91)
/// </summary>
public enum PdfTilingType
{
    /// <summary>
    /// Unknown or unrecognized tiling type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Tiles are spaced consistently; pattern cell may be distorted to achieve constant spacing.
    /// </summary>
    ConstantSpacing = 1,

    /// <summary>
    /// Tiles are rendered without distortion; spacing between tiles may vary slightly.
    /// </summary>
    NoDistortion = 2,

    /// <summary>
    /// Like <see cref="ConstantSpacing"/> but the renderer may use a faster, lower-fidelity approach.
    /// </summary>
    ConstantSpacingFast = 3
}
