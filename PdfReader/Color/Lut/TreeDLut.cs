using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Color.Structures;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfReader.Color.Lut;

/// <summary>
/// Helper for building and sampling 3D device->sRGB lookup tables.
/// LUT layout: contiguous packed RGB triples for each lattice point in (R,G,B) iteration order.
/// Trilinear sampling uses precomputed weights that sum to 1 (normalized floats).
/// </summary>
internal sealed class TreeDLut : IRgbaSampler
{
    private readonly Vector3[] _lut;
    private readonly ThreeDLutProfile _profile;

    private TreeDLut(Vector3[] lut, ThreeDLutProfile profile)
    {
        _lut = lut;
        _profile = profile;
    }

    /// <summary>
    /// Builds a packed Vector3 (device to sRGB) sampled uniformly over each dimension.
    /// The LUT layout order is R (outer), G (middle), B (inner) for memory locality.
    /// Each Vector3 is stored as (R, G, B) mapping directly to pixel components.
    /// </summary>
    /// <param name="intent">The rendering intent controlling device to sRGB conversion.</param>
    /// <param name="converter">Delegate converting normalized device color to sRGB SKColor.</param>
    /// <param name="lutSize">The desired size of the LUT along each dimension (e.g., 16 for a 16x16x16 LUT).</param>
    /// <returns>A new <see cref="TreeDLut"/> instance containing the sampled LUT, or null if converter is null.</returns>
    public static TreeDLut Build(PdfRenderingIntent intent, DeviceToSrgbCore converter, int lutSize = 16)
    {
        if (converter == null)
        {
            return default;
        }

        var profile = new ThreeDLutProfile(lutSize);

        int n = profile.GridSize;
        int totalPoints = n * n * n;
        Vector3[] lut = new Vector3[totalPoints];
        Span<float> input = stackalloc float[3];

        int writeIndex = 0;
        for (int rIndex = 0; rIndex < n; rIndex++)
        {
            float rNorm = (float)rIndex / (n - 1);
            for (int gIndex = 0; gIndex < n; gIndex++)
            {
                float gNorm = (float)gIndex / (n - 1);
                for (int bIndex = 0; bIndex < n; bIndex++)
                {
                    float bNorm = (float)bIndex / (n - 1);

                    input[0] = rNorm;
                    input[1] = gNorm;
                    input[2] = bNorm;

                    SKColor color = converter(input, intent);

                    // Store as R,G,B (X=R, Y=G, Z=B).
                    lut[writeIndex] = new Vector3(color.Red, color.Green, color.Blue);
                    writeIndex++;
                }
            }
        }

        return new TreeDLut(lut, profile);
    }

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <summary>
    /// Performs trilinear sampling of the LUT for a single pixel.
    /// The source pixel's R, G, and B values are used to sample the LUT.
    /// The result is written to the destination pixel.
    /// </summary>
    /// <param name="source">The source pixel to sample (R, G, B components).</param>
    /// <param name="destination">The destination pixel to receive the sampled color.</param>
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        Sample(ref _lut[0], _profile, source, ref destination);
    }

    /// <summary>
    /// Performs trilinear interpolation over R, G, B using the provided LUT.
    /// The LUT must be a packed array of Vector3 values in (R, G, B) order.
    /// The source pixel's R, G, and B values are used to sample the LUT.
    /// The result is written to the destination pixel.
    /// </summary>
    /// <param name="lut">Reference to the first element of the LUT array.</param>
    /// <param name="source">The source pixel to sample (R, G, B components).</param>
    /// <param name="destination">The destination pixel to receive the sampled color.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sample(ref Vector3 lut, ThreeDLutProfile profile, ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        ref var sourceRef = ref MemoryMarshal.GetReference(source);
        var sourceVector = Unsafe.As<float, Vector3>(ref sourceRef);
        var normalized = Vector3.Clamp(sourceVector, Vector3.Zero, Vector3.One);
        var scaled = normalized * (profile.GridSize - 1);

        // Clamp once with a cached vector to avoid repeated construction.
        var scaledClamped = Vector3.Clamp(scaled, Vector3.Zero, profile.LatticeMax);

        // Integer lattice coordinates packed in a vector.
        Vector3 sourceLattice = new Vector3((int)scaledClamped.X, (int)scaledClamped.Y, (int)scaledClamped.Z);

        // Fractional part vector and its complement.
        var fraction = scaled - sourceLattice;
        var one = Vector3.One;
        var complement = one - fraction; // (wR0, wG0, wB0)

        float wR0 = complement.X;
        float wG0 = complement.Y;
        float wB0 = complement.Z;

        float wR1 = fraction.X;
        float wG1 = fraction.Y;
        float wB1 = fraction.Z;

        // Base (r,g,b) lattice point linear index in packed layout (R outer, G middle, B inner).
        // Use a vector dot with stride vector for compact calculation.
        int baseIndex = (int)Vector3.Dot(sourceLattice, profile.Strides);

        // Fetch lattice colors.
        ref Vector3 c000 = ref Unsafe.Add(ref lut, baseIndex); // (r0,g0,b0)
        ref Vector3 c001 = ref Unsafe.Add(ref c000, 1); // i001
        ref Vector3 c100 = ref Unsafe.Add(ref c000, profile.TripleStrideR); // (r1,g0,b0)
        ref Vector3 c101 = ref Unsafe.Add(ref c100, 1); // i101
        ref Vector3 c010 = ref Unsafe.Add(ref c000, profile.TripleStrideG); // (r0,g1,b0)
        ref Vector3 c011 = ref Unsafe.Add(ref c010, 1); // i011
        ref Vector3 c110 = ref Unsafe.Add(ref c000, profile.TripleStrideRG); // (r1,g1,b0)
        ref Vector3 c111 = ref Unsafe.Add(ref c110, 1); // i111

        // Compute trilinear interpolation.
        // IMPORTANT: DON'T OPTIMIZE/SIMPLIFY THIS EXPRESSION FURTHER. This insures compiler generates optimal SIMD code.
        Vector3 accum =
            c000 * (wR0 * wG0 * wB0) +
            c100 * (wR1 * wG0 * wB0) +
            c010 * (wR0 * wG1 * wB0) +
            c110 * (wR1 * wG1 * wB0) +
            c001 * (wR0 * wG0 * wB1) +
            c101 * (wR1 * wG0 * wB1) +
            c011 * (wR0 * wG1 * wB1) +
            c111 * (wR1 * wG1 * wB1);

        destination = new RgbaPacked((byte)accum.X, (byte)accum.Y, (byte)accum.Z, 255);
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
