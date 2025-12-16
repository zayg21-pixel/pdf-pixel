using System;
using SkiaSharp;
using System.Numerics;
using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Lut;
using PdfReader.Color.Structures;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for PDF /Lab (CIEBasedLab) color space (NOT ICCBased).
/// </summary>
internal sealed class LabColorSpaceConverter : PdfColorSpaceConverter
{
    private static readonly Vector4 _labOffset = new Vector4(0, 128, 128, 0);
    private static readonly Vector4 _labScale = new Vector4(1f / 100, 1f / 255, 1f / 255, 1);
    private static readonly Vector4 _defaultMin = new Vector4(0f, -100f, -100f, 0f);
    private static readonly Vector4 _defaultMax = new Vector4(100f, 100f, 100f, 1f);

    private readonly IIccTransform _normalizeTransform;
    private readonly IIccTransform _labTransform;

    public LabColorSpaceConverter(float[] whitePoint, float[] blackPoint, float[] rangeArray)
    {
        Vector4 whitePointVector;
        if (whitePoint != null && whitePoint.Length >= 3)
        {
            whitePointVector = IccVectorUtilities.ToVector4(whitePoint);
        }
        else
        {
            whitePointVector = IccTransforms.D65WhitePoint;
        }

        Vector4 labMinVector;
        Vector4 labMaxVector;

        if (rangeArray != null && rangeArray.Length >= 4)
        {
            labMinVector = new Vector4(0f, rangeArray[0], rangeArray[2], 0);
            labMaxVector = new Vector4(100f, rangeArray[1], rangeArray[3], 1);
        }
        else
        {
            labMinVector = _defaultMin;
            labMaxVector = _defaultMax;
        }

        var normalizeTransform = new IccFunctionTransform(x => (Vector4.Clamp(x, labMinVector, labMaxVector) + _labOffset) * _labScale);
        var toXyzTransform = IccTransforms.BuildLabToXyzTransform(whitePointVector);
        var toSrgbTransform = IccTransforms.BuildXyzToSrgbTransform(whitePointVector);

        _normalizeTransform = normalizeTransform;
        _labTransform = new IccChainedTransform(toXyzTransform, toSrgbTransform);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps, PdfRenderingIntent intent)
    {
        var labVector = IccVectorUtilities.ToVector4(comps);

        _normalizeTransform.Transform(ref labVector);
        _labTransform.Transform(ref labVector);

        byte R = ToByte(labVector.X);
        byte G = ToByte(labVector.Y);
        byte B = ToByte(labVector.Z);

        return new SKColor(R, G, B);
    }

    private SKColor NormalizedToSrgbCore(ReadOnlySpan<float> comps, PdfRenderingIntent intent)
    {
        var labVector = IccVectorUtilities.ToVector4(comps);
        _labTransform.Transform(ref labVector);

        byte R = ToByte(labVector.X);
        byte G = ToByte(labVector.Y);
        byte B = ToByte(labVector.Z);

        return new SKColor(R, G, B);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        var lut = ThreeDLut.Build(intent, NormalizedToSrgbCore);
        return new LabSampler(_normalizeTransform, lut);
    }

    private sealed class LabSampler : IRgbaSampler
    {
        private readonly IIccTransform _normalizeTransform;
        private readonly ThreeDLut _threeDLut;

        public LabSampler(IIccTransform normalizeTransform, ThreeDLut threeDLut)
        {
            _normalizeTransform = normalizeTransform;
            _threeDLut = threeDLut;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
        {
            var labVector = IccVectorUtilities.ToVector4(source);
            _normalizeTransform.Transform(ref labVector);
            var buffer = CreateSpanOver(ref labVector);
            _threeDLut.Sample(buffer, ref destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SKColor SampleColor(ReadOnlySpan<float> source)
        {
            var labVector = IccVectorUtilities.ToVector4(source);
            _normalizeTransform.Transform(ref labVector);
            var buffer = CreateSpanOver(ref labVector);
            return _threeDLut.SampleColor(buffer);
        }

        public bool IsDefault { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<float> CreateSpanOver(ref Vector4 v)
        {
#if NETSTANDARD2_0
            unsafe
            {
                ref float first = ref Unsafe.As<Vector4, float>(ref v);
                void* ptr = Unsafe.AsPointer(ref first);
                return new Span<float>(ptr, 4);
            }
#else
            ref float first = ref Unsafe.As<Vector4, float>(ref v);
            return MemoryMarshal.CreateSpan(ref first, 4);
#endif
        }
    }
}
