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

    private readonly IccFunctionTransform _normalizeTransform;
    private readonly IccChainedTransform _labTransform;

    public LabColorSpaceConverter(float[] whitePoint, float[] blackPoint, float[] rangeArray)
    {
        Vector4 whitePointVector;
        if (whitePoint != null && whitePoint.Length >= 3)
        {
            whitePointVector = IccVectorUtilities.ToVector4WithOnePadding(whitePoint);
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

        var normalizeTransform = new IccFunctionTransform(x => (x + _labOffset) * _labScale);
        var toXyzTransform = IccTransforms.BuildLabToXyzTransform(whitePointVector);
        var toSrgbTransform = IccTransforms.BuildXyzToSrgbTransform(whitePointVector);

        _normalizeTransform = normalizeTransform;
        _labTransform = new IccChainedTransform(toXyzTransform, toSrgbTransform);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps, PdfRenderingIntent intent)
    {
        var labVector = IccVectorUtilities.ToVector4WithOnePadding(comps);

        _normalizeTransform.Transform(ref labVector);
        _labTransform.Transform(ref labVector);

        byte R = ToByte(labVector.X);
        byte G = ToByte(labVector.Y);
        byte B = ToByte(labVector.Z);

        return new SKColor(R, G, B);
    }

    private SKColor NormalizedToSrgbCore(ReadOnlySpan<float> comps, PdfRenderingIntent intent)
    {
        var labVector = IccVectorUtilities.ToVector4WithOnePadding(comps);
        _labTransform.Transform(ref labVector);

        byte R = ToByte(labVector.X);
        byte G = ToByte(labVector.Y);
        byte B = ToByte(labVector.Z);

        return new SKColor(R, G, B);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        var labClut = IccClutTransform.Build(intent, NormalizedToSrgbCore, 3, 3);
        return new LabSampler(_normalizeTransform, labClut);
    }

    private class LabSampler : IRgbaSampler
    {
        private readonly IccFunctionTransform _normalizeFunction;
        private readonly IccClutTransform _labClut;

        public LabSampler(IccFunctionTransform normalizeFunction, IccClutTransform labClut)
        {
            _normalizeFunction = normalizeFunction;
            _labClut = labClut;
        }

        public bool IsDefault => false;

        public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
        {
            var labVector = IccVectorUtilities.ToVector4WithOnePadding(source);
            _normalizeFunction.Transform(ref labVector);
            _labClut.Transform(ref labVector);
            labVector = labVector * 255f;
            destination = new RgbaPacked((byte)labVector.X, (byte)labVector.Y, (byte)labVector.Z, 255);
        }

        public SKColor SampleColor(ReadOnlySpan<float> source)
        {
            var labVector = IccVectorUtilities.ToVector4WithOnePadding(source);
            _normalizeFunction.Transform(ref labVector);
            _labClut.Transform(ref labVector);
            labVector = labVector * 255f;

            return new SKColor((byte)labVector.X, (byte)labVector.Y, (byte)labVector.Z);
        }
    }
}
