using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Model;

/// <summary>
/// Parameters controlling JPEG 2000 decoding resolution.
/// Allows decoding at reduced resolution by skipping DWT decomposition levels,
/// which is significantly faster and uses less memory than full-resolution decoding
/// followed by downscaling.
/// </summary>
public readonly struct JpxDecodingParameters
{
    /// <summary>
    /// The resolution reduction factor, expressed as a power of 2.
    /// <list type="bullet">
    ///   <item><description>1 — full resolution (no reduction).</description></item>
    ///   <item><description>2 — half resolution (skip 1 DWT level).</description></item>
    ///   <item><description>4 — quarter resolution (skip 2 DWT levels).</description></item>
    /// </list>
    /// Must be a power of 2 and at least 1.
    /// </summary>
    public int DescaleFactor { get; }

    /// <summary>
    /// The number of finest DWT decomposition levels to skip during inverse transform.
    /// Computed as <c>log2(<see cref="DescaleFactor"/>)</c>.
    /// </summary>
    public int LevelsToSkip { get; }

    /// <summary>
    /// Regions, in full-resolution (not descaled) image coordinates, that must be decoded.
    /// Tiles outside every region are skipped and left as placeholder (zeroed) data.
    /// Null means every tile must be decoded.
    /// </summary>
    public IReadOnlyList<JpxRectangle>? RegionsOfInterest { get; }

    /// <summary>
    /// Default parameters representing full-resolution decoding of the entire image.
    /// </summary>
    public static JpxDecodingParameters Default { get; } = new(1);

    /// <summary>
    /// Initializes decoding parameters with the given descale factor and regions of interest.
    /// </summary>
    /// <param name="descaleFactor">Power-of-2 reduction factor. Must be &gt;= 1.</param>
    /// <param name="regionsOfInterest">Regions, in full-resolution image coordinates, that must be decoded. Null means every tile must be decoded.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="descaleFactor"/> is less than 1 or not a power of 2.</exception>
    public JpxDecodingParameters(int descaleFactor, IReadOnlyList<JpxRectangle>? regionsOfInterest = null)
    {
        if (descaleFactor < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(descaleFactor), "Descale factor must be at least 1.");
        }

        if ((descaleFactor & (descaleFactor - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descaleFactor), "Descale factor must be a power of 2.");
        }

        DescaleFactor = descaleFactor;
        LevelsToSkip = Log2(descaleFactor);
        RegionsOfInterest = regionsOfInterest;
    }

    /// <summary>
    /// Computes the reduced dimension for a given full-resolution dimension.
    /// </summary>
    /// <param name="fullDimension">Full-resolution size in pixels.</param>
    /// <returns>Reduced size, always at least 1.</returns>
    public int ReduceDimension(int fullDimension)
    {
        if (DescaleFactor <= 1)
        {
            return fullDimension;
        }

        return Math.Max(1, (fullDimension + DescaleFactor - 1) / DescaleFactor);
    }

    private static int Log2(int value)
    {
        int result = 0;

        while (value > 1)
        {
            value >>= 1;
            result++;
        }

        return result;
    }
}
