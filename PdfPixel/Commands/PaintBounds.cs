using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Operations on the area a command paints, where <see langword="null"/> stands for unbounded.
/// </summary>
internal static class PaintBounds
{
    /// <summary>
    /// Returns the overlapping region of <paramref name="first"/> and <paramref name="second"/>: the other one when one
    /// is unbounded, and <see langword="null"/> when both are.
    /// </summary>
    public static PdfRectangle? Intersect(PdfRectangle? first, PdfRectangle? second)
    {
        if (first == null)
        {
            return second;
        }
        else if (second == null)
        {
            return first;
        }
        else
        {
            return PdfRectangle.Intersect(first.Value, second.Value);
        }
    }
}
