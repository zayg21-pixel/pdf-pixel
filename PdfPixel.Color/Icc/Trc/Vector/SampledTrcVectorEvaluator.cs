using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Evaluator for sampled TRC for 4 channels at once.
/// </summary>
internal sealed class SampledTrcVectorEvaluator : IIccTrcVectorEvaluator
{
    private readonly int _channelsCount;
    private readonly float[] _samples0;
    private readonly float[] _samples1;
    private readonly float[] _samples2;
    private readonly float[] _samples3;
    private readonly Vector4 _scale;

    public SampledTrcVectorEvaluator(float[][] samples)
    {
        if (samples == null || samples.Length == 0 || samples.Length > 4)
        {
            throw new ArgumentException("samples must be an array of 1 to 4 float[]", nameof(samples));
        }

        _channelsCount = samples.Length;
        _samples0 = (samples.Length > 0) ? samples[0] : [];
        _samples1 = (samples.Length > 1) ? samples[1] : [];
        _samples2 = (samples.Length > 2) ? samples[2] : [];
        _samples3 = (samples.Length > 3) ? samples[3] : [];

        switch (_channelsCount)
        {
            case 1:
                {
                    _scale = new Vector4(_samples0.Length - 1, 1f, 1f, 1f);
                    break;
                }
            case 2:
                {
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, 1f, 1f);
                    break;
                }
            case 3:
                {
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, 1f);
                    break;
                }
            default:
                {
                    _scale = new Vector4(_samples0.Length - 1, _samples1.Length - 1, _samples2.Length - 1, _samples3.Length - 1);
                    break;
                }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Evaluate(Vector4 x)
    {
        Vector4 scaled = x * _scale;
        scaled = Vector4.Clamp(scaled, Vector4.Zero, _scale);

        switch (_channelsCount)
        {
            case 1:
            {
                var idxX = (int)scaled.X;
                float r = _samples0[idxX];
                return new Vector4(r, 1f, 1f, 1f);
            }
            case 2:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                float r = _samples0[idxX];
                float g = _samples1[idxY];
                return new Vector4(r, g, 1f, 1f);
            }
            case 3:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                var idxZ = (int)scaled.Z;
                float r = _samples0[idxX];
                float g = _samples1[idxY];
                float b = _samples2[idxZ];
                return new Vector4(r, g, b, 1f);
            }
            default:
            {
                var idxX = (int)scaled.X;
                var idxY = (int)scaled.Y;
                var idxZ = (int)scaled.Z;
                var idxW = (int)scaled.W;
                float r = _samples0[idxX];
                float g = _samples1[idxY];
                float b = _samples2[idxZ];
                float a = _samples3[idxW];
                return new Vector4(r, g, b, a);
            }
        }
    }
}
