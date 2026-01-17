using PdfRender.Color.Structures;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Transform;

/// <summary>
/// Provides utility methods for converting and manipulating color vectors and matrices for color transformations.
/// </summary>
internal static class ColorVectorUtilities
{
    private static readonly Vector4 MaxByte = new Vector4(255f);
    private static readonly Vector4 ByteOffset = new Vector4(0.5f);

    /// <summary>
    /// Converts a 3x3 float matrix to a 4x4 matrix suitable for use with <see cref="System.Numerics.Matrix4x4"/>.
    /// </summary>
    /// <param name="matrix3x3">A 3x3 matrix as a two-dimensional float array.</param>
    /// <returns>A 4x4 matrix with the 3x3 values in the upper-left and the rest padded appropriately.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 ToMatrix4x4(float[,] matrix3x3)
    {
        return new Matrix4x4(
            matrix3x3[0, 0], matrix3x3[0, 1], matrix3x3[0, 2], 0,
            matrix3x3[1, 0], matrix3x3[1, 1], matrix3x3[1, 2], 0,
            matrix3x3[2, 0], matrix3x3[2, 1], matrix3x3[2, 2], 0,
            0, 0, 0, 1);
    }

    /// <summary>
    /// Converts a span of floats to a <see cref="Vector4"/>, padding with 1.0 for missing components.
    /// </summary>
    /// <param name="data">Input span of float values (0-4 elements).</param>
    /// <returns>A <see cref="Vector4"/> with missing elements padded with 1.0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToVector4WithOnePadding(ReadOnlySpan<float> data)
    {
        return data.Length switch
        {
            0 => Vector4.One,
            1 => new Vector4(data[0], 1, 1, 1),
            2 => new Vector4(data[0], data[1], 1, 1),
            3 => new Vector4(data[0], data[1], data[2], 1),
            _ => new Vector4(data[0], data[1], data[2], data[3]),
        };
    }

    /// <summary>
    /// Converts a normalized <see cref="Vector4"/> (0-1 range) to a packed RGBA byte structure.
    /// </summary>
    /// <param name="source">Source color vector (0-1 range).</param>
    /// <param name="destination">Destination packed RGBA value (by reference).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load01ToRgba(Vector4 source, ref RgbaPacked destination)
    {
        var scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;

        destination.R = (byte)scaled.X;
        destination.G = (byte)scaled.Y;
        destination.B = (byte)scaled.Z;
        destination.A = 255;
    }

    /// <summary>
    /// Converts a Vector4 with components in the range [0, 1] to an RgbaPacked structure with 8-bit per channel color
    /// values.
    /// </summary>
    /// <param name="source">The source vector containing normalized color components, where each component should be in the range [0, 1].</param>
    /// <returns>An RgbaPacked structure representing the color, with each channel mapped to the 0–255 byte range and the alpha
    /// channel set to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaPacked From01ToRgba(this Vector4 source)
    {
        var scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;
        return new RgbaPacked
        {
            R = (byte)scaled.X,
            G = (byte)scaled.Y,
            B = (byte)scaled.Z,
            A = 255
        };
    }

    /// <summary>
    /// Converts a Vector4 with color channel values in the range [0, 1] to an SKColor with 8-bit per channel RGB
    /// values.
    /// </summary>
    /// <param name="source">A Vector4 representing the source color, where the X, Y, and Z components correspond to the red, green, and blue
    /// channels, respectively. Each component should be in the range [0, 1].</param>
    /// <returns>An SKColor representing the equivalent color with 8-bit RGB channels. The alpha channel is set to fully opaque.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor From01ToSkiaColor(this Vector4 source)
    {
        var scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;
        return new SKColor(
            (byte)scaled.X,
            (byte)scaled.Y,
            (byte)scaled.Z,
            255);
    }


    /// <summary>
    /// Converts a span of floats to a <see cref="Vector4"/>, padding with 0.0 for missing components.
    /// </summary>
    /// <param name="data">Input span of float values (0-4 elements).</param>
    /// <returns>A <see cref="Vector4"/> with missing elements padded with 0.0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToVector4WithZeroPadding(ReadOnlySpan<float> data)
    {
        return data.Length switch
        {
            0 => Vector4.One,
            1 => new Vector4(data[0], 0, 0, 0),
            2 => new Vector4(data[0], data[1], 0, 0),
            3 => new Vector4(data[0], data[1], data[2], 0),
            _ => new Vector4(data[0], data[1], data[2], data[3]),
        };
    }

    /// <summary>
    /// Custom dot product implementation for Vector4 that guarantees
    /// that at least multiplication operation would be vectorized.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CustomDot(Vector4 a, Vector4 b)
    {
        // .NET runtime prefers DPPS instruction for Dot, which is slower than VDPPS by a huge margin.
        // to avoid that, we implement our own version of Dot that uses
        // multiply + manual sum implementation which is surprisingly extremely fast and as fast as VDPPS.
        var ab = a * b;
        return ab.X + ab.Y + ab.Z + ab.W;
    }
}
