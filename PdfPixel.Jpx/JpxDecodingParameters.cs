using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx;

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
    /// The components that are reconstructed, by index in the codestream, in the order they are output. Components left
    /// out are never entropy decoded, transformed or allocated. Null means every component, the one the cdef box
    /// declares as opacity last.
    /// </summary>
    public IReadOnlyList<int>? ComponentSelection { get; }

    /// <summary>
    /// Default parameters representing full-resolution decoding of the entire image.
    /// </summary>
    public static JpxDecodingParameters Default { get; } = new(1);

    /// <summary>
    /// Initializes decoding parameters with the given descale factor and component selection.
    /// </summary>
    /// <param name="descaleFactor">Power-of-2 reduction factor. Must be &gt;= 1.</param>
    /// <param name="componentSelection">The components to reconstruct, in output order; null for every component.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="descaleFactor"/> is less than 1 or not a power of 2.</exception>
    public JpxDecodingParameters(int descaleFactor, IReadOnlyList<int>? componentSelection = null)
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
        ComponentSelection = componentSelection;
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
