namespace PdfPixel.Imaging.Jpg.Decoding;

/// <summary>
/// Utility to de-zigzag an 8x8 block from JPEG zig-zag order to natural (row-major) order.
/// </summary>
internal static class JpgZigZag
{
    /// <summary>
    /// Zig-zag index to natural (row-major) index mapping for an 8x8 block.
    /// Exposed for direct indexing in hot paths.
    /// </summary>
    internal static readonly byte[] Table =
    [
        0,  1,  8, 16,  9,  2,  3, 10,
       17, 24, 32, 25, 18, 11,  4,  5,
       12, 19, 26, 33, 40, 48, 41, 34,
       27, 20, 13,  6,  7, 14, 21, 28,
       35, 42, 49, 56, 57, 50, 43, 36,
       29, 22, 15, 23, 30, 37, 44, 51,
       58, 59, 52, 45, 38, 31, 39, 46,
       53, 60, 61, 54, 47, 55, 62, 63
    ];
}
