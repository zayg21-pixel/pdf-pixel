using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Color.Structures;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Lut;

/// <summary>
/// Layered CMY 3D LUT representation sampled at multiple K (black) levels with linear interpolation across K.
/// Each slice is a regular 3D grid (same layout as TreeDLut) built with CMY varying and fixed K.
/// Sampling: full trilinear interpolation (C,M,Y) inside two adjacent K slices using Vector3 LUTs, then linear blend across K.
/// </summary>
internal sealed class LayeredThreeDLut : IRgbaSampler
{
    private readonly Vector3[][] _kSlices; // Array of CMY 3D LUTs (one per sampled K). Each slice stores packed Vector3 RGB values.
    private readonly float[] _kLevels;     // Normalized K level positions for slices (0..1 ascending).
    private readonly ThreeDLutProfile _profile;

    private LayeredThreeDLut(Vector3[][] kSlices, float[] kLevels, ThreeDLutProfile profile)
    {
        _kSlices = kSlices;
        _kLevels = kLevels;
        _profile = profile;
    }

    /// <summary>
    /// Builds layered CMY LUTs for a predefined set of K sample levels.
    /// Stores each slice as a Vector3[] (R,G,B) lattice for trilinear sampling.
    /// </summary>
    /// <param name="intent">Rendering intent controlling device to sRGB conversion.</param>
    /// <param name="converter">Delegate converting CMYK (normalized 0..1) to sRGB SKColor.</param>
    /// <param name="lutSize">Size of the LUT (default is 16).</param>
    /// <param name="layerCount">Number of K (black) layers to sample (default is 8).</param>
    internal static LayeredThreeDLut Build(PdfRenderingIntent intent, DeviceToSrgbCore converter, int lutSize = 16, int layerCount = 8)
    {
        if (converter == null)
        {
            return null;
        }

        // Tunable K sampling distribution (denser near ends for smoother highlight / shadow transitions).
        float[] kLevels = new float[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            kLevels[i] = (float)i / (layerCount - 1);
        }

        int sliceCount = kLevels.Length;
        var slices = new Vector3[sliceCount][];

        int pointCount = lutSize * lutSize * lutSize;
        var profile = new ThreeDLutProfile(lutSize);

        Span<float> components = stackalloc float[4]; // CMYK

        for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
        {
            float kValue = kLevels[sliceIndex];
            var slice = new Vector3[pointCount];
            int writeIndex = 0;

            for (int cIndex = 0; cIndex < lutSize; cIndex++)
            {
                float c = (float)cIndex / (lutSize - 1);
                for (int mIndex = 0; mIndex < lutSize; mIndex++)
                {
                    float m = (float)mIndex / (lutSize - 1);
                    for (int yIndex = 0; yIndex < lutSize; yIndex++)
                    {
                        float y = (float)yIndex / (lutSize - 1);

                        components[0] = c;
                        components[1] = m;
                        components[2] = y;
                        components[3] = kValue;

                        var color = converter(components, intent);

                        slice[writeIndex] = new Vector3(color.Red, color.Green, color.Blue);
                        writeIndex++;
                    }
                }
            }

            slices[sliceIndex] = slice;
        }

        return new LayeredThreeDLut(slices, kLevels, profile);
    }

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <summary>
    /// Samples a single CMYK pixel using layered 3D LUTs and writes the result to the destination pixel.
    /// Uses the K (A channel) from the source to select/interpolate between K slices.
    /// Sets the output alpha channel to 255 (opaque).
    /// </summary>
    /// <param name="source">Source pixel (R=C, G=M, B=Y, A=K)</param>
    /// <param name="destination">Destination pixel (R=R, G=G, B=B, A=255)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        if (_kSlices == null || _kLevels == null)
        {
            return;
        }

        float kNormalized = source[3];
        int upperSliceIndex = 0;
        while (upperSliceIndex < _kLevels.Length && _kLevels[upperSliceIndex] < kNormalized)
        {
            upperSliceIndex++;
        }

        if (upperSliceIndex == 0)
        {
            ThreeDLut.Sample(ref _kSlices[0][0], _profile, source, ref destination);
        }
        else if (upperSliceIndex >= _kLevels.Length)
        {
            ThreeDLut.Sample(ref _kSlices[_kLevels.Length - 1][0], _profile, source, ref destination);
        }
        else
        {
            int lowerSliceIndex = upperSliceIndex - 1;

            float kLower = _kLevels[lowerSliceIndex];
            float kUpper = _kLevels[upperSliceIndex];
            float t = kUpper <= kLower ? 0f : (kNormalized - kLower) / (kUpper - kLower);
            if (t < 0f)
            {
                t = 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
            }

            RgbaPacked lowerSample = default;
            RgbaPacked upperSample = default;
            ThreeDLut.Sample(ref _kSlices[lowerSliceIndex][0], _profile, source, ref lowerSample);
            ThreeDLut.Sample(ref _kSlices[upperSliceIndex][0], _profile, source, ref upperSample);

            float blendLowerWeight = 1f - t;
            float blendUpperWeight = t;

            Vector3 lowerVector = new Vector3(lowerSample.R, lowerSample.G, lowerSample.B);
            Vector3 upperVector = new Vector3(upperSample.R, upperSample.G, upperSample.B);
            Vector3 blendedVector = (lowerVector * blendLowerWeight) + (upperVector * blendUpperWeight);

            destination.R = (byte)(blendedVector.X + 0.5f);
            destination.G = (byte)(blendedVector.Y + 0.5f);
            destination.B = (byte)(blendedVector.Z + 0.5f);
        }

        destination.A = 255; // Force opaque alpha
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
