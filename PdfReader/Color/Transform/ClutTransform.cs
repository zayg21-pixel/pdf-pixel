using PdfReader.Color.Sampling;
using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Transform;

internal sealed partial class ClutTransform : IColorTransform, IRgbaSampler
{
    private const double MinWeight = 10e-6;

    private static readonly Vector4[] MaskTable = new Vector4[16]
    {
        new Vector4(0f, 0f, 0f, 0f), // 0
        new Vector4(1f, 0f, 0f, 0f), // 1
        new Vector4(0f, 1f, 0f, 0f), // 2
        new Vector4(1f, 1f, 0f, 0f), // 3
        new Vector4(0f, 0f, 1f, 0f), // 4
        new Vector4(1f, 0f, 1f, 0f), // 5
        new Vector4(0f, 1f, 1f, 0f), // 6
        new Vector4(1f, 1f, 1f, 0f), // 7
        new Vector4(0f, 0f, 0f, 1f), // 8
        new Vector4(1f, 0f, 0f, 1f), // 9
        new Vector4(0f, 1f, 0f, 1f), // 10
        new Vector4(1f, 1f, 0f, 1f), // 11
        new Vector4(0f, 0f, 1f, 1f), // 12
        new Vector4(1f, 0f, 1f, 1f), // 13
        new Vector4(0f, 1f, 1f, 1f), // 14
        new Vector4(1f, 1f, 1f, 1f)  // 15
    };

    private readonly Vector4[] _clut;
    private readonly int _outChannels;
    private readonly int[] _gridPointsPerDimension;
    private readonly int[] _strides;
    private readonly int _dimensionCount;
    private readonly int _actualDimensions;
    private readonly Vector4 _scaleFactors; // Pre-computed scaling factors for vectorization
    private readonly Vector4 _maxValues;    // Pre-computed max values for clamping
    private readonly Vector4 _dimensionEnabled; // 1 for enabled dims, 0 otherwise
    private readonly Vector4 _oneMinusDimensionEnabled;
    private readonly Vector4 _strideVector; // Strides for up to 4 dims (disabled dims are 0)

    // Cached vertex data to avoid per-call work
    private readonly int _vertexCount;
    private readonly Vector4[] _vertexMasks; // Only masks relevant for actual dimensions
    private readonly int[] _vertexStrideDots; // Dot(maskVec, _strideVector) per vertex

    public ClutTransform(float[] clut, int outChannels, int[] gridPointsPerDimension)
        : this(ConvertFloatArrayToVector4Array(clut, outChannels), outChannels, gridPointsPerDimension)
    {
    }

    public ClutTransform(Vector4[] clut, int outChannels, int[] gridPointsPerDimension)
    {
        _outChannels = outChannels;
        _gridPointsPerDimension = gridPointsPerDimension;
        _dimensionCount = gridPointsPerDimension.Length;
        _actualDimensions = Math.Min(_dimensionCount, 4);

        // Pre-compute strides for better performance
        _strides = new int[_dimensionCount];
        int cumulative = 1; // Each entry is now a Vector4, not individual floats
        for (int d = _dimensionCount - 1; d >= 0; d--)
        {
            _strides[d] = cumulative;
            int grid = gridPointsPerDimension[d];
            cumulative *= grid;
        }

        // Pre-compute vectorized scaling factors and max values using utility with zero padding
        Span<float> scaleValues = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            scaleValues[i] = i < _dimensionCount && _gridPointsPerDimension[i] > 1 
                ? _gridPointsPerDimension[i] - 1 
                : 0f;
        }
        _scaleFactors = ColorVectorUtilities.ToVector4WithZeroPadding(scaleValues);
        _maxValues = _scaleFactors - new Vector4(10e-5f); // TODO: verify small epsilon

        // Precompute dimension enable mask using utility with zero padding
        Span<float> dimensionMask = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            dimensionMask[i] = i < _actualDimensions ? 1f : 0f;
        }
        _dimensionEnabled = ColorVectorUtilities.ToVector4WithZeroPadding(dimensionMask);
        _oneMinusDimensionEnabled = Vector4.One - _dimensionEnabled;

        // Precompute stride vector for offset dot-product using utility with zero padding
        Span<float> strideValues = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            strideValues[i] = i < _actualDimensions && i < _strides.Length 
                ? _strides[i] 
                : 0f;
        }
        _strideVector = ColorVectorUtilities.ToVector4WithZeroPadding(strideValues);

        // Compute vertex cache
        _vertexCount = 1 << _actualDimensions;
        _vertexMasks = new Vector4[_vertexCount];
        _vertexStrideDots = new int[_vertexCount];
        for (int mask = 0; mask < _vertexCount; mask++)
        {
            Vector4 maskVec = MaskTable[mask];
            _vertexMasks[mask] = maskVec;
            _vertexStrideDots[mask] = (int)Vector4.Dot(maskVec, _strideVector);
        }

        // Use the provided Vector4 array directly
        _clut = clut ?? throw new ArgumentNullException(nameof(clut));
        
        // Validate that the clut array has the expected size
        int expectedSize = cumulative;
        if (_clut.Length != expectedSize)
        {
            throw new ArgumentException($"CLUT array size mismatch. Expected {expectedSize} entries, got {_clut.Length}.", nameof(clut));
        }
    }

    private static Vector4[] ConvertFloatArrayToVector4Array(float[] clut, int outChannels)
    {
        if (clut == null)
        {
            throw new ArgumentNullException(nameof(clut));
        }

        int totalEntries = clut.Length / outChannels;
        Vector4[] vector4Clut = new Vector4[totalEntries];

        for (int i = 0; i < totalEntries; i++)
        {
            int baseIndex = i * outChannels;
            ReadOnlySpan<float> channelSpan = clut.AsSpan(baseIndex, Math.Min(outChannels, clut.Length - baseIndex));
            vector4Clut[i] = ColorVectorUtilities.ToVector4WithOnePadding(channelSpan);
        }

        return vector4Clut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var vector = ColorVectorUtilities.ToVector4WithOnePadding(source);

        //Transform(ref vector);
        switch (_actualDimensions)
        {
            case 3:
                vector = Transform3D(vector); // TODO: add missing
                break;
            case 4:
                vector = Transform4D(vector);
                break;
            default:
                vector = Transform(vector);
                break;
        }

        vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255f;

        destination.R = (byte)vector.X;
        destination.G = (byte)vector.Y;
        destination.B = (byte)vector.Z;
        destination.A = 255;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        switch (_actualDimensions)
        {
            case 3:
                return Transform3D(color);
            case 4:
                return Transform4D(color);
        }


        Vector4 scaledPositions = color * _scaleFactors;
        scaledPositions = Vector4.Clamp(scaledPositions, Vector4.Zero, _maxValues);

        Vector4 flooredPositions = new Vector4(
            (int)scaledPositions.X,
            (int)scaledPositions.Y,
            (int)scaledPositions.Z,
            (int)scaledPositions.W
        );
        
        Vector4 fractions = scaledPositions - flooredPositions;
        Vector4 oneMinusFractions = Vector4.One - fractions;

        int baseOffset = (int)Vector4.Dot(flooredPositions, _strideVector);

        Vector4 diff = fractions - oneMinusFractions;

        Vector4 result = Vector4.Zero;
        int vertexCount = _vertexCount;

        Vector4 dimensionEnabled = _dimensionEnabled;

        ref Vector4 vertexMasksRef = ref _vertexMasks[0];
        ref int vertexStrideDotsRef = ref _vertexStrideDots[0];
        ref Vector4 clutRef = ref _clut[0];

        for (int mask = 0; mask < vertexCount; mask++)
        {
            Vector4 selected = oneMinusFractions + diff * Unsafe.Add(ref vertexMasksRef, mask);

            selected = selected * dimensionEnabled + _oneMinusDimensionEnabled;

            float weight = selected.X * selected.Y * selected.Z * selected.W;

            if (weight < MinWeight)
            {
                continue;
            }

            int offset = baseOffset + Unsafe.Add(ref vertexStrideDotsRef, mask);

            result += Unsafe.Add(ref clutRef, offset) * weight;
        }

        return result;
    }

    // Tetrahedral interpolation specialized for 3D CLUTs (branchless sorting network)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform3D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _maxValues);

        Vector4 floored = new Vector4((int)scaled.X, (int)scaled.Y, (int)scaled.Z, 0f);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)Vector4.Dot(floored, _strideVector);

        float a = frac.X; int sa = _strides[0];
        float b = frac.Y; int sb = _strides[1];
        float c = frac.Z; int sc = _strides[2];

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

    // Tetrahedral interpolation specialized for 4D CLUTs (5-vertex simplex inside hypercube)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector4 Transform4D(Vector4 color)
    {
        Vector4 scaled = color * _scaleFactors;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _maxValues);

        Vector4 floored = new Vector4((int)scaled.X, (int)scaled.Y, (int)scaled.Z, (int)scaled.W);
        Vector4 frac = scaled - floored;

        int baseOffset = (int)Vector4.Dot(floored, _strideVector);

        float a = frac.X; int sa = _strides[0];
        float b = frac.Y; int sb = _strides[1];
        float c = frac.Z; int sc = _strides[2];
        float d = frac.W; int sd = _strides[3];

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
