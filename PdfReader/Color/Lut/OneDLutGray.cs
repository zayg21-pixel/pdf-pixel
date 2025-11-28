using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Color.Structures;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Lut;

/// <summary>
/// Helper for building and sampling 1D grayscale -> sRGB lookup tables.
/// LUT layout: packed Vector3 for each lattice point in gray iteration order.
/// Linear sampling uses precomputed weights that sum to 1 (normalized floats).
/// </summary>
internal sealed class OneDLutGray : IRgbaSampler
{
    internal const int GridSize = 64; // 64 lattice points
    private const int GridIndexShift = 6; // Upper 6 bits: lattice index, lower 2 bits: fractional component
    private const int GridIndexMask = 0x3F; // Mask for fractional component (0..63)
    private const float Inv63 = 1f / 63f; // Fraction conversion for index (0..63 -> 0..1)

    private readonly Vector3[] _lut;

    private OneDLutGray(Vector3[] lut)
    {
        _lut = lut;
    }

    /// <summary>
    /// Builds a packed Vector3 (gray to sRGB) sampled uniformly over the gray axis.
    /// Each Vector3 is stored as (R, G, B) mapping directly to pixel components.
    /// </summary>
    /// <param name="intent">The rendering intent controlling device to sRGB conversion.</param>
    /// <param name="converter">Delegate converting normalized device color to sRGB SKColor.</param>
    /// <returns>A new <see cref="OneDLutGray"/> instance containing the sampled LUT, or null if converter is null.</returns>
    public static OneDLutGray Build(PdfRenderingIntent intent, DeviceToSrgbCore converter)
    {
        if (converter == null)
        {
            return default;
        }
        int n = GridSize;
        Vector3[] lut = new Vector3[n];
        Span<float> input = stackalloc float[1];

        for (int i = 0; i < n; i++)
        {
            float grayNorm = (float)i / (n - 1);
            input[0] = grayNorm;
            SKColor color = converter(input, intent);
            lut[i] = new Vector3(color.Red, color.Green, color.Blue);
        }
        return new OneDLutGray(lut);
    }

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <summary>
    /// Performs linear sampling of the LUT for a single gray pixel.
    /// The source pixel's first value is used to sample the LUT.
    /// The result is written to the destination pixel.
    /// </summary>
    /// <param name="source">The source pixel to sample (gray component in [0,1]).</param>
    /// <param name="destination">The destination pixel to receive the sampled color.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        Sample(ref _lut[0], source, ref destination);
    }

    /// <summary>
    /// Performs linear interpolation over gray using the provided LUT.
    /// The LUT must be a packed array of Vector3 values.
    /// The source pixel's first value is used to sample the LUT.
    /// The result is written to the destination pixel.
    /// </summary>
    /// <param name="lut">Reference to the first element of the LUT array.</param>
    /// <param name="source">The source pixel to sample (gray component in [0,1]).</param>
    /// <param name="destination">The destination pixel to receive the sampled color.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sample(ref Vector3 lut, ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        float gray = source[0];
        float scaled = gray * (GridSize - 1);
        int index = (int)scaled;
        float frac = scaled - index;

        ref Vector3 c0 = ref Unsafe.Add(ref lut, index);
        ref Vector3 c1 = ref lut;
        if (index < GridSize - 1)
        {
            c1 = ref Unsafe.Add(ref lut, index + 1);
        }
        else
        {
            c1 = ref c0;
        }

        Vector3 accum = c0 * (1f - frac) + c1 * frac;
        destination.R = (byte)accum.X;
        destination.G = (byte)accum.Y;
        destination.B = (byte)accum.Z;
        destination.A = 255;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SKColor SampleColor(ReadOnlySpan<float> source)
    {
        RgbaPacked destination = new RgbaPacked();
        Sample(source, ref destination);
        return new SKColor(destination.R, destination.G, destination.B, destination.A);
    }
}
