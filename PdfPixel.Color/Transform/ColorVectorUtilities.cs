using PdfPixel.Color.Structures;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Provides utility methods for converting and manipulating color vectors and matrices for color transformations.
/// </summary>
public static class ColorVectorUtilities
{
    private static readonly Vector4 MaxByte = new(255f);
    private static readonly Vector4 ByteOffset = new(0.5f);
    private static readonly Vector4 InverseMaxByte = new(1f / 255f);

    /// <summary>
    /// Converts a 3x3 float matrix to a 4x4 matrix suitable for use with <see cref="Matrix4x4"/>.
    /// </summary>
    /// <param name="matrix3x3">A 3x3 matrix as a two-dimensional float array.</param>
    /// <returns>A 4x4 matrix with the 3x3 values in the upper-left and the rest padded appropriately.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 ToMatrix4x4(float[,] matrix3x3)
    {
        if (matrix3x3 == null)
        {
            throw new ArgumentNullException(nameof(matrix3x3));
        }

        return new Matrix4x4(
            matrix3x3[0, 0],
            matrix3x3[0, 1],
            matrix3x3[0, 2],
            0,
            matrix3x3[1, 0],
            matrix3x3[1, 1],
            matrix3x3[1, 2],
            0,
            matrix3x3[2, 0],
            matrix3x3[2, 1],
            matrix3x3[2, 2],
            0,
            0,
            0,
            0,
            1);
    }

    /// <summary>
    /// Converts a span of floats to a <see cref="Vector4"/>, padding with 1.0 for missing components.
    /// </summary>
    /// <param name="data">Input span of float values (0-4 elements).</param>
    /// <returns>A <see cref="Vector4"/> with missing elements padded with 1.0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToVector4WithOnePadding(in ReadOnlySpan<float> data)
    {
        return data.Length switch
        {
            0 => Vector4.One,
            1 => new Vector4(data[0], 1f, 1f, 1f),
            2 => new Vector4(data[0], data[1], 1f, 1f),
            3 => new Vector4(data[0], data[1], data[2], 1f),
            _ => new Vector4(data[0], data[1], data[2], data[3])
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
        Vector4 scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;

        destination.R = (byte)scaled.X;
        destination.G = (byte)scaled.Y;
        destination.B = (byte)scaled.Z;
        destination.A = 255;
    }

    /// <summary>
    /// Converts a normalized <see cref="Vector4"/> (0-1 range) to the color channels of a packed RGBA
    /// value, leaving its alpha channel untouched.
    /// </summary>
    /// <param name="source">Source color vector (0-1 range).</param>
    /// <param name="destination">Destination packed RGBA value (by reference).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load01ToRgb(Vector4 source, ref RgbaPacked destination)
    {
        Vector4 scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;

        destination.R = (byte)scaled.X;
        destination.G = (byte)scaled.Y;
        destination.B = (byte)scaled.Z;
    }

    /// <summary>
    /// Converts a normalized <see cref="Vector4"/> (0-1 range) to a packed RGB byte structure.
    /// </summary>
    /// <param name="source">Source color vector (0-1 range).</param>
    /// <param name="destination">Destination packed RGB value (by reference).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Load01ToRgb(Vector4 source, ref RgbPacked destination)
    {
        Vector4 scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;

        destination.R = (byte)scaled.X;
        destination.G = (byte)scaled.Y;
        destination.B = (byte)scaled.Z;
    }

    /// <summary>
    /// Converts a packed RGBA value to a normalized <see cref="Vector4"/> (0-1 range), alpha included.
    /// </summary>
    /// <param name="source">Source packed RGBA value.</param>
    /// <returns>A <see cref="Vector4"/> holding the four channels scaled to the 0-1 range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 FromRgbaTo01(this RgbaPacked source)
        => new Vector4(source.R, source.G, source.B, source.A) * InverseMaxByte;

    /// <summary>
    /// Converts a span of floats to a <see cref="Vector4"/>, padding with 0.0 for missing components.
    /// </summary>
    /// <param name="data">Input span of float values (0-4 elements).</param>
    /// <returns>A <see cref="Vector4"/> with missing elements padded with 0.0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToVector4WithZeroPadding(in ReadOnlySpan<float> data)
    {
        return data.Length switch
        {
            0 => Vector4.One,
            1 => new Vector4(data[0], 0, 0, 0),
            2 => new Vector4(data[0], data[1], 0, 0),
            3 => new Vector4(data[0], data[1], data[2], 0),
            _ => new Vector4(data[0], data[1], data[2], data[3])
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
        Vector4 ab = a * b;
        return ab.X + ab.Y + ab.Z + ab.W;
    }
}
