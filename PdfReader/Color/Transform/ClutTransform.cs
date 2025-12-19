using PdfReader.Color.Sampling;
using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using SkiaSharp;
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
        _maxValues = _scaleFactors; // Same values for clamping

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

    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var vector = ColorVectorUtilities.ToVector4WithOnePadding(source);
        Transform(ref vector);
        vector *= 255f;
        destination = new RgbaPacked((byte)vector.X, (byte)vector.Y, (byte)vector.Z, (byte)255f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Transform(ref Vector4 color)
    {
        Vector4 scaledPositions = color * _scaleFactors;
        scaledPositions = Vector4.Clamp(scaledPositions, Vector4.Zero, _maxValues);

        // Pre-calculate factors per dimension for vectorized operations
        Vector4 flooredPositions = new Vector4(
            (int)scaledPositions.X,
            (int)scaledPositions.Y, 
            (int)scaledPositions.Z,
            (int)scaledPositions.W
        );
        
        Vector4 fractions = scaledPositions - flooredPositions;
        Vector4 oneMinusFractions = Vector4.One - fractions;

        // Compute base offset via dot-product with stride vector
        int baseOffset = (int)Vector4.Dot(flooredPositions, _strideVector);

        // Precompute difference for selection: select = g + (f - g) * mask
        Vector4 diff = fractions - oneMinusFractions;

        // Initialize result vector
        Vector4 result = Vector4.Zero;
        int vertexCount = _vertexCount;

        // Use precomputed dimensionEnabled
        Vector4 dimensionEnabled = _dimensionEnabled;

        // Get references to array start for unsafe operations
        ref Vector4 vertexMasksRef = ref _vertexMasks[0];
        ref int vertexStrideDotsRef = ref _vertexStrideDots[0];
        ref Vector4 clutRef = ref _clut[0];

        for (int mask = 0; mask < vertexCount; mask++)
        {
            // Select per-dimension contribution: oneMinus + (fraction - oneMinus) * mask
            Vector4 selected = oneMinusFractions + diff * Unsafe.Add(ref vertexMasksRef, mask);

            // Neutralize disabled dimensions without branches
            selected = selected * dimensionEnabled + _oneMinusDimensionEnabled;

            float weight = selected.X * selected.Y * selected.Z * selected.W;

            if (weight < MinWeight)
            {
                continue;
            }

            // Compute offset using cached stride dot-products with unsafe access
            int offset = baseOffset + Unsafe.Add(ref vertexStrideDotsRef, mask);

            result += Unsafe.Add(ref clutRef, offset) * weight;
        }

        // Copy result back, respecting output channel count
        color = result;
    }
}
