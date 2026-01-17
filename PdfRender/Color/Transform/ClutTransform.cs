using PdfRender.Color.Sampling;
using PdfRender.Color.Transform;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Icc.Transform;

/// <summary>
/// Performs color look-up table (CLUT) transformations with efficient multi-dimensional interpolation for ICC color profiles.
/// Supports 1D to 4D CLUTs and vectorized operations for high-performance color space conversion.
/// </summary>
internal sealed partial class ClutTransform : IColorTransform, IRgbaSampler
{
    private readonly Vector4[] _clut;
    private readonly int _outChannels;
    private readonly int[] _gridPointsPerDimension;
    private readonly int[] _strides;
    private readonly int _dimensionCount;
    private readonly int _actualDimensions;
    private readonly Vector4 _scaleFactors; // Pre-computed scaling factors for vectorization
    private readonly float _scaleX;
    private readonly float _scaleY;
    private readonly float _scaleZ;
    private readonly float _scaleW;
    private readonly Vector4 _strideVector; // Strides for up to 4 dims (disabled dims are 0)

    /// <summary>
    /// Initializes a new instance of the <see cref="ClutTransform"/> class from a float array CLUT.
    /// </summary>
    /// <param name="clut">Flat array of CLUT values.</param>
    /// <param name="outChannels">Number of output channels per CLUT entry.</param>
    /// <param name="gridPointsPerDimension">Number of grid points per dimension.</param>
    public ClutTransform(float[] clut, int outChannels, int[] gridPointsPerDimension)
        : this(ConvertFloatArrayToVector4Array(clut, outChannels), outChannels, gridPointsPerDimension)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClutTransform"/> class from a Vector4 array CLUT.
    /// </summary>
    /// <param name="clut">Array of Vector4 CLUT entries.</param>
    /// <param name="outChannels">Number of output channels per CLUT entry.</param>
    /// <param name="gridPointsPerDimension">Number of grid points per dimension.</param>
    public ClutTransform(Vector4[] clut, int outChannels, int[] gridPointsPerDimension)
    {
        _outChannels = outChannels;
        _gridPointsPerDimension = gridPointsPerDimension;
        _dimensionCount = gridPointsPerDimension.Length;
        _actualDimensions = Math.Min(_dimensionCount, 4);

        _strides = new int[_dimensionCount];
        int cumulative = 1;
        for (int d = _dimensionCount - 1; d >= 0; d--)
        {
            _strides[d] = cumulative;
            int grid = gridPointsPerDimension[d];
            cumulative *= grid;
        }

        Span<float> scaleValues = stackalloc float[4];
        for (int i = 0; i < 4; i++)
        {
            scaleValues[i] = i < _dimensionCount && _gridPointsPerDimension[i] > 1 
                ? _gridPointsPerDimension[i] - 1 
                : 0f;
        }
        _scaleFactors = ColorVectorUtilities.ToVector4WithZeroPadding(scaleValues);
        _scaleX = scaleValues[0];
        _scaleY = scaleValues[1];
        _scaleZ = scaleValues[2];
        _scaleW = scaleValues[3];
        _strideVector = ColorVectorUtilities.ToVector4WithZeroPadding(_strides.Select(x => (float)x).ToArray());

        _clut = clut ?? throw new ArgumentNullException(nameof(clut));
        
        int expectedSize = cumulative;
        if (_clut.Length != expectedSize)
        {
            throw new ArgumentException($"CLUT array size mismatch. Expected {expectedSize} entries, got {_clut.Length}.", nameof(clut));
        }
    }

    public bool IsIdentity => false;

    /// <summary>
    /// Converts a flat float array CLUT to a Vector4 array, padding with 1.0 if needed.
    /// </summary>
    /// <param name="clut">Flat array of CLUT values.</param>
    /// <param name="outChannels">Number of output channels per CLUT entry.</param>
    /// <returns>Array of Vector4 CLUT entries.</returns>
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

    /// <summary>
    /// Samples the CLUT using the provided source color and writes the result to the destination.
    /// </summary>
    /// <param name="source">Source color as a span of floats.</param>
    /// <param name="destination">Destination packed RGBA value (by reference).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        Vector4 vector = ColorVectorUtilities.ToVector4WithOnePadding(source);
        return Transform(vector);
    }

    /// <summary>
    /// Transforms the input color using the CLUT and returns the interpolated color.
    /// </summary>
    /// <param name="color">Input color vector.</param>
    /// <returns>Transformed color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        return _actualDimensions switch
        {
            1 => Transform1D(color),
            2 => Transform2D(color),
            3 => Transform3D(color),
            4 => Transform4D(color),
            _ => color,
        };
    }
}
