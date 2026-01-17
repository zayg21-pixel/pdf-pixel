using PdfRender.Color.Transform;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Icc.Transform;

/// <summary>
/// Performs color look-up table (CLUT) transformations using multi-dimensional interpolation for ICC color profiles.
/// </summary>
internal sealed partial class ClutTransform
{
    /// <summary>
    /// Performs 1D linear interpolation on the CLUT for the given color vector.
    /// </summary>
    /// <param name="color">Input color vector.</param>
    /// <returns>Interpolated color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform1D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _scaleFactors);

        Vector4 floored = new Vector4((int)scaled.X, 0f, 0f, 0f);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)ColorVectorUtilities.CustomDot(floored, _strideVector);

        float a = frac.X;
        int sa = floored.X == _scaleX ? 0 : _strides[0];

        float w0 = 1f - a;
        float w1 = a;

        int o0 = baseOffset;
        int o1 = o0 + sa;

        ref Vector4 clutRef = ref _clut[0];
        return
            Unsafe.Add(ref clutRef, o0) * w0 +
            Unsafe.Add(ref clutRef, o1) * w1;
    }

    /// <summary>
    /// Performs 2D barycentric interpolation on the CLUT for the given color vector.
    /// </summary>
    /// <param name="color">Input color vector.</param>
    /// <returns>Interpolated color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform2D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _scaleFactors);

        Vector4 floored = new Vector4((int)scaled.X, (int)scaled.Y, 0f, 0f);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)ColorVectorUtilities.CustomDot(floored, _strideVector);

        float a = frac.X;
        int sa = floored.X == _scaleX ? 0 : _strides[0];
        float b = frac.Y;
        int sb = floored.Y == _scaleY ? 0 : _strides[1];

        if (a < b)
        {
            (b, a) = (a, b);
            (sb, sa) = (sa, sb);
        }

        float w0 = 1f - a;
        float w1 = a - b;
        float w2 = b;

        int o0 = baseOffset;
        int o1 = baseOffset + sa;
        int o2 = o1 + sb;

        ref Vector4 clutRef = ref _clut[0];
        return
            Unsafe.Add(ref clutRef, o0) * w0 +
            Unsafe.Add(ref clutRef, o1) * w1 +
            Unsafe.Add(ref clutRef, o2) * w2;
    }

    /// <summary>
    /// Performs 3D barycentric interpolation on the CLUT for the given color vector.
    /// </summary>
    /// <param name="color">Input color vector.</param>
    /// <returns>Interpolated color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform3D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _scaleFactors);

        Vector4 floored = new Vector4((int)scaled.X, (int)scaled.Y, (int)scaled.Z, 0f);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)ColorVectorUtilities.CustomDot(floored, _strideVector);

        float a = frac.X; int sa = floored.X == _scaleX ? 0 : _strides[0];
        float b = frac.Y; int sb = floored.Y == _scaleY ? 0 : _strides[1];
        float c = frac.Z; int sc = floored.Z == _scaleZ ? 0 : _strides[2];

        if (a < b)
        {
            (b, a) = (a, b);
            (sb, sa) = (sa, sb);
        }
        if (b < c)
        {
            (c, b) = (b, c);
            (sc, sb) = (sb, sc);
        }
        if (a < b)
        {
            (b, a) = (a, b);
            (sb, sa) = (sa, sb);
        }

        float w0 = 1f - a;
        float w1 = a - b;
        float w2 = b - c;
        float w3 = c;

        int o0 = baseOffset;
        int o1 = baseOffset + sa;
        int o2 = o1 + sb;
        int o3 = o2 + sc;

        ref Vector4 clutRef = ref _clut[0];
        return
            Unsafe.Add(ref clutRef, o0) * w0 +
            Unsafe.Add(ref clutRef, o1) * w1 +
            Unsafe.Add(ref clutRef, o2) * w2 +
            Unsafe.Add(ref clutRef, o3) * w3;
    }

    /// <summary>
    /// Performs 4D barycentric interpolation on the CLUT for the given color vector.
    /// </summary>
    /// <param name="color">Input color vector.</param>
    /// <returns>Interpolated color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform4D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _scaleFactors);

        Vector4 floored = new Vector4((int)scaled.X, (int)scaled.Y, (int)scaled.Z, (int)scaled.W);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)ColorVectorUtilities.CustomDot(floored, _strideVector);

        float a = frac.X; int sa = floored.X == _scaleX ? 0 : _strides[0];
        float b = frac.Y; int sb = floored.Y == _scaleY ? 0 : _strides[1];
        float c = frac.Z; int sc = floored.Z == _scaleZ ? 0 : _strides[2];
        float d = frac.W; int sd = floored.W == _scaleW ? 0 : _strides[3];

        if (a < b)
        {
            (b, a) = (a, b);
            (sb, sa) = (sa, sb);
        }
        if (c < d)
        {
            (d, c) = (c, d);
            (sd, sc) = (sc, sd);
        }
        if (a < c)
        {
            (c, a) = (a, c);
            (sc, sa) = (sa, sc);
        }
        if (b < d)
        {
            (d, b) = (b, d);
            (sd, sb) = (sb, sd);
        }
        if (b < c)
        {
            (c, b) = (b, c);
            (sc, sb) = (sb, sc);
        }

        float w0 = 1f - a;
        float w1 = a - b;
        float w2 = b - c;
        float w3 = c - d;
        float w4 = d;

        int o0 = baseOffset;
        int o1 = baseOffset + sa;
        int o2 = o1 + sb;
        int o3 = o2 + sc;
        int o4 = o3 + sd;

        ref Vector4 clutRef = ref _clut[0];
        return
            Unsafe.Add(ref clutRef, o0) * w0 +
            Unsafe.Add(ref clutRef, o1) * w1 +
            Unsafe.Add(ref clutRef, o2) * w2 +
            Unsafe.Add(ref clutRef, o3) * w3 +
            Unsafe.Add(ref clutRef, o4) * w4;
    }
}
