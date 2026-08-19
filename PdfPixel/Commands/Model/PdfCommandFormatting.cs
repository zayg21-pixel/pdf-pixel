using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Formats command state for <see cref="object.ToString"/> overrides, for debugging.
/// </summary>
internal static class PdfCommandFormatting
{
    /// <summary>
    /// Formats a matrix in short PDF <c>[a b c d e f]</c> operand order, for debugging.
    /// </summary>
    public static string FormatMatrix(in PdfMatrix matrix)
        => $"[{matrix.ScaleX:0.###} {matrix.SkewY:0.###} {matrix.SkewX:0.###} {matrix.ScaleY:0.###} {matrix.TransX:0.###} {matrix.TransY:0.###}]";

    /// <summary>
    /// Formats a paint's blend mode, color, and style, for debugging.
    /// </summary>
    public static string FormatPaint(PdfPaint paint)
        => $"{paint.BlendMode}/{paint.Color}/{paint.Style}";
}
