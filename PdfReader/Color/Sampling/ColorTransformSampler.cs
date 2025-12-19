using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class ColorTransformSampler : IRgbaSampler
{
    private static readonly Vector4 NormalizeOffset = new Vector4(0.5f);
    private bool _isNoOpTransform;
    private readonly PixelProcessorCallback _processorCallback;

    public ColorTransformSampler(ChainedColorTransform chainedTransform)
    {
        _processorCallback = chainedTransform.GetCallback();
        _isNoOpTransform = _processorCallback == null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var value = ColorVectorUtilities.ToVector4WithOnePadding(source);

        if (!_isNoOpTransform)
        {
            _processorCallback(ref value);
        }
        var scaled = Vector4.Clamp(value, Vector4.Zero, Vector4.One) * 255f + NormalizeOffset;

        destination.R = (byte)scaled.X;
        destination.G = (byte)scaled.Y;
        destination.B = (byte)scaled.Z;
        destination.A = 255;
    }
}
