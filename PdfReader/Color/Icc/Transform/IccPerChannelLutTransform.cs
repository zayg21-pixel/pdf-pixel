using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Transform;

// TODO: Optimize memory usage and performance
internal class IccPerChannelLutTransform : IIccTransform
{
    private readonly LutChannelInfo[] _perChannelLuts;

    public IccPerChannelLutTransform(float[][] luts)
    {
        _perChannelLuts = new LutChannelInfo[luts.Length];

        for (int i = 0; i < luts.Length; i++)
        {
            _perChannelLuts[i] = new LutChannelInfo(luts[i]);
        }
    }

    public IccPerChannelLutTransform(IccTrc[] trcs, int lutSize = 1024)
    {
        _perChannelLuts = new LutChannelInfo[trcs.Length];

        for (int i = 0; i < trcs.Length; i++)
        {
            _perChannelLuts[i] = new LutChannelInfo(trcs[i].ToLut(lutSize));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Transform(ref Vector4 color)
    {
        ref float colorRef = ref Unsafe.As<Vector4, float>(ref color);
        ref LutChannelInfo lutRef = ref _perChannelLuts[0];

        for (int i = 0; i < _perChannelLuts.Length; i++)
        {
            if (!lutRef.IsIdentity)
            {
                colorRef = EvaluateLut(in lutRef, colorRef);
            }
            else if (lutRef.UseSimpleIndex)
            {
                var index = (int)(colorRef * lutRef.LastIndex + 0.5f);
#if NET8_0_OR_GREATER
                index = Math.Clamp(index, 0, lutRef.LastIndex);
#else
                index = index > lutRef.LastIndex ? lutRef.LastIndex : index < 0 ? 0 : index;
#endif
                colorRef = lutRef.Lut[index];
            }

            colorRef = ref Unsafe.Add(ref colorRef, 1);
            lutRef = ref Unsafe.Add(ref lutRef, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EvaluateLut(in LutChannelInfo channel, float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }

        if (value <= 0f)
        {
            return channel.Lut[0];
        }

        if (value >= 1f)
        {
            return channel.Lut[channel.LastIndex];
        }

        float position = value * channel.LastIndex;
        int index0 = (int)position;
        int index1 = index0 + 1;
        float fraction = position - index0;

        float v0 = channel.Lut[index0];
        float v1 = channel.Lut[index1];

#if NET8_0_OR_GREATER
        return MathF.FusedMultiplyAdd(fraction, v1 - v0, v0);
#else
        return v0 + fraction * (v1 - v0);
#endif
    }

    private readonly struct LutChannelInfo
    {
        public readonly float[] Lut;
        public readonly bool IsIdentity;
        public readonly int LastIndex;
        public readonly bool UseSimpleIndex;

        public LutChannelInfo(float[] lut)
        {
            Lut = lut;
            LastIndex = lut.Length - 1;
            IsIdentity = IsIdentityLut(lut);
            UseSimpleIndex = lut.Length >= 256;
        }
    }

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
