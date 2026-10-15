namespace PdfPixel.Png.Decoding;

/// <summary>
/// Geometry of the seven Adam7 passes (PNG Third Edition, section 8.2).
/// </summary>
internal static class PngAdam7
{
    public const int PassCount = 7;

    public static readonly int[] StartX = [0, 4, 0, 2, 0, 1, 0];
    public static readonly int[] StartY = [0, 0, 4, 0, 2, 0, 1];
    public static readonly int[] StepX = [8, 8, 4, 4, 2, 2, 1];
    public static readonly int[] StepY = [8, 8, 8, 4, 4, 2, 2];

    /// <summary>
    /// Returns the number of pixels a pass holds in each of its rows, 0 when the pass is empty.
    /// </summary>
    public static int GetPassWidth(int pass, int width) => (width > StartX[pass]) ? (width - StartX[pass] + StepX[pass] - 1) / StepX[pass] : 0;

    /// <summary>
    /// Returns the number of rows a pass holds, 0 when the pass is empty.
    /// </summary>
    public static int GetPassHeight(int pass, int height) => (height > StartY[pass]) ? (height - StartY[pass] + StepY[pass] - 1) / StepY[pass] : 0;
}
