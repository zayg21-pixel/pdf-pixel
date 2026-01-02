using System.Numerics;
using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;
using PdfReader.Color.Icc;

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

    private readonly FunctionColorTransform _normalizeTransform;
    private readonly ChainedColorTransform _labTransform;

    public LabColorSpaceConverter(float[] whitePoint, float[] blackPoint, float[] rangeArray)
    {
        Vector4 whitePointVector;
        if (whitePoint != null && whitePoint.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
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

        var normalizeTransform = new FunctionColorTransform(x => (Vector4.Clamp(x, labMinVector, labMaxVector) + _labOffset) * _labScale);
        var toXyzTransform = IccTransforms.BuildLabToXyzTransform(whitePointVector);
        var toSrgbTransform = IccTransforms.BuildXyzToSrgbTransform(whitePointVector);

        _normalizeTransform = normalizeTransform;
        _labTransform = new ChainedColorTransform(toXyzTransform, toSrgbTransform);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        var chained = new ChainedColorTransform(_normalizeTransform, _labTransform, postTransform);
        return new ColorTransformSampler(chained);
    }
}
