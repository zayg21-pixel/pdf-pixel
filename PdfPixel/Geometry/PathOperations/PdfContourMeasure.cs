using System.Collections.Generic;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// One contour of a <see cref="PdfPathMeasure"/>.
/// </summary>
public sealed class PdfContourMeasure
{
    internal PdfContourMeasure()
    {
    }

    /// <summary>
    /// Whether the contour is closed.
    /// </summary>
    public bool IsClosed { get; internal set; }

    internal List<IPdfContourSegment> Segments { get; } = [];

    /// <summary>
    /// Appends this contour to <paramref name="builder"/> as a new subpath, closed when <see cref="IsClosed"/>.
    /// </summary>
    internal void AppendTo(PdfPathBuilder builder)
    {
        if (Segments.Count > 0)
        {
            builder.MoveTo(PdfVectorMath.ToPdfPoint(Segments[0].Start));

            for (int index = 0; index < Segments.Count; index++)
            {
                Segments[index].AppendTo(builder);
            }

            if (IsClosed)
            {
                builder.Close();
            }
        }
    }
}
