using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using System;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Converts the whole root layer into <paramref name="samples"/>.
    /// </summary>
    /// <param name="samples">The samples to write, the size of the surface.</param>
    public void ToImageSamples(PdfImageSamples samples) => ToImageSamples(new PdfIntegerRectangle(0, 0, Width, Height), samples);

    /// <summary>
    /// Converts <paramref name="region"/> of the root layer into <paramref name="samples"/>, as
    /// <see cref="PdfImageSamplesConverter"/> converts the root's premultiplied float samples; samples without alpha
    /// receive the root as composited over black.
    /// </summary>
    /// <param name="region">The pixels to convert, within the surface.</param>
    /// <param name="samples">
    /// The samples to write, the size of <paramref name="region"/>: any of the root's components and its alpha, in any
    /// order.
    /// </param>
    public void ToImageSamples(in PdfIntegerRectangle region, PdfImageSamples samples)
    {
        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        if (region.Left < 0 || region.Top < 0 || region.Right > Width || region.Bottom > Height || region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "The region must be a non-empty part of the surface.");
        }

        if (samples.Width != region.Width || samples.Height != region.Height)
        {
            throw new ArgumentException("The samples must be the size of the region.", nameof(samples));
        }

        PdfImageSamples result = _root.Result;
        PdfImageSamplesConverter converter = new(result, samples);
        int pixelBytes = result.RowBytes / Width;

        for (int row = 0; row < region.Height; row++)
        {
            converter.Convert(result.GetRow(region.Top + row).Slice(region.Left * pixelBytes, region.Width * pixelBytes), samples.GetRow(row));
        }
    }
}
