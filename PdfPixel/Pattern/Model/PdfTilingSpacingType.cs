namespace PdfPixel.Pattern.Model;

/// <summary>
/// Strongly typed tiling type (PDF spec Table 91)
/// </summary>
public enum PdfTilingSpacingType
{
    None = 0,
    ConstantSpacing = 1,
    NoDistortion = 2,
    ConstantSpacingFast = 3
}
