namespace PdfPixel.Functions;

/// <summary>
/// Enumerates supported PDF function types.
/// </summary>
public enum PdfFunctionType
{
    Unknown = -1,
    Sampled = 0,
    Exponential = 2,
    Stitching = 3,
    PostScript = 4
}
