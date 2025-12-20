using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform;

internal sealed class PerChannelLutTransform : IColorTransform
{
    private readonly float[] _channel1Lut;
    private readonly float[] _channel2Lut;
    private readonly float[] _channel3Lut;
    private readonly float[] _channel4Lut;
    private readonly int _channelCount;
    private readonly bool _isPassthrough;
    private readonly int _lutSize;
    private readonly int _lastIndex;
    private readonly float _scaleFactor;
    private readonly Vector4 _maxIndices;
    private const int StandardLutSize = 1024;

    public PerChannelLutTransform(float[][] luts, int preferredLutSize = StandardLutSize)
    {
        if (luts == null || luts.Length == 0)
        {
            _channelCount = 0;
            _isPassthrough = true;
            return;
        }

        // Limit to maximum 4 channels early
        float[][] trimmedLuts;
        if (luts.Length > 4)
        {
            trimmedLuts = new float[4][];
            Array.Copy(luts, trimmedLuts, 4);
        }
        else
        {
            trimmedLuts = luts;
        }
        
        var normalizedLuts = NormalizeLuts(trimmedLuts, preferredLutSize);
        _channelCount = normalizedLuts.Length;
        _isPassthrough = IsPassthroughTransform(normalizedLuts);
        
        // Assign to individual channel arrays for better cache locality
        _channel1Lut = _channelCount > 0 ? normalizedLuts[0] : null;
        _channel2Lut = _channelCount > 1 ? normalizedLuts[1] : null;
        _channel3Lut = _channelCount > 2 ? normalizedLuts[2] : null;
        _channel4Lut = _channelCount > 3 ? normalizedLuts[3] : null;
        
        if (!_isPassthrough)
        {
            _lutSize = _channel1Lut.Length;
            _lastIndex = _lutSize - 1;
            _scaleFactor = _lastIndex + 0.5f;
            _maxIndices = new Vector4(_lastIndex);
        }
    }

    public PerChannelLutTransform(IccTrc[] trcs, int preferredLutSize = StandardLutSize)
        : this(ConvertTrcsToLuts(trcs, preferredLutSize), preferredLutSize)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float[][] ConvertTrcsToLuts(IccTrc[] trcs, int preferredLutSize)
    {
        if (trcs == null || trcs.Length == 0)
        {
            return null;
        }

        // Limit to maximum 4 channels early
        int channelCount = Math.Min(trcs.Length, 4);
        var luts = new float[channelCount][];
        for (int i = 0; i < channelCount; i++)
        {
            luts[i] = trcs[i].ToLut(preferredLutSize);
        }
        
        return luts;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        if (_isPassthrough)
        {
            return color;
        }

        // Calculate indices for all channels at once using vectorized operations
        Vector4 scaledColor = color * _scaleFactor;
        
        // Clamp all indices at once using Vector4.Clamp with cached values
        Vector4 clampedIndices = Vector4.Clamp(scaledColor, Vector4.Zero, _maxIndices);

        // Use switch statement for direct channel assignment based on channel count
        // Writing all 4 components at once is faster than individual assignments
        return _channelCount switch
        {
            1 => new Vector4(_channel1Lut[(int)clampedIndices.X], 1, 1, 1),
            2 => new Vector4(_channel1Lut[(int)clampedIndices.X], _channel2Lut[(int)clampedIndices.Y], 1, 1),
            3 => new Vector4(_channel1Lut[(int)clampedIndices.X], _channel2Lut[(int)clampedIndices.Y], _channel3Lut[(int)clampedIndices.Z], 1),
            4 => new Vector4(_channel1Lut[(int)clampedIndices.X], _channel2Lut[(int)clampedIndices.Y], _channel3Lut[(int)clampedIndices.Z], _channel4Lut[(int)clampedIndices.W]),
            _ => color,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float[][] NormalizeLuts(float[][] luts, int preferredLutSize)
    {
        if (luts == null || luts.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        // Check if all LUTs have the same size and meet the preferred size
        bool needsNormalization = false;
        int firstSize = luts[0].Length;
        
        // Check if any LUT has different size or is smaller than preferred
        if (firstSize < preferredLutSize)
        {
            needsNormalization = true;
        }
        else
        {
            for (int i = 1; i < luts.Length; i++)
            {
                if (luts[i].Length != firstSize)
                {
                    needsNormalization = true;
                    break;
                }
            }
        }

        // If normalization needed, rescale all to preferred size
        if (needsNormalization)
        {
            var normalizedLuts = new float[luts.Length][];
            for (int i = 0; i < luts.Length; i++)
            {
                normalizedLuts[i] = RescaleLut(luts[i], preferredLutSize);
            }
            return normalizedLuts;
        }

        return luts;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float[] RescaleLut(float[] originalLut, int newSize)
    {
        if (originalLut.Length == newSize)
        {
            return originalLut;
        }

        var rescaledLut = new float[newSize];
        int originalLastIndex = originalLut.Length - 1;
        int newLastIndex = newSize - 1;

        for (int i = 0; i < newSize; i++)
        {
            float position = (float)i / newLastIndex * originalLastIndex;
            int index0 = (int)position;
            
            if (index0 >= originalLastIndex)
            {
                rescaledLut[i] = originalLut[originalLastIndex];
            }
            else
            {
                int index1 = index0 + 1;
                float fraction = position - index0;
                float v0 = originalLut[index0];
                float v1 = originalLut[index1];

#if NET8_0_OR_GREATER
                rescaledLut[i] = MathF.FusedMultiplyAdd(fraction, v1 - v0, v0);
#else
                rescaledLut[i] = v0 + fraction * (v1 - v0);
#endif
            }
        }

        return rescaledLut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPassthroughTransform(float[][] luts)
    {
        foreach (var lut in luts)
        {
            if (!IsIdentityLut(lut))
            {
                return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentityLut(float[] lut)
    {
        int length = lut.Length;
        int lastIndex = length - 1;
        
        for (int i = 0; i < length; i++)
        {
            float expected = (float)i / lastIndex;
            if (MathF.Abs(lut[i] - expected) > 1e-6f)
            {
                return false;
            }
        }

        return true;
    }
}
