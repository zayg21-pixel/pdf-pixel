using System.Numerics;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Color.Icc;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Lab color space: a lightness component and two chromatic components, a* and b*, in the
/// CIE 1976 L*a*b* space.
/// </summary>
public sealed class PdfLabColorSpaceConverter : PdfColorSpaceConverter
{
    private static readonly Vector4 _labOffset = new(0, 128, 128, 0);
    private static readonly Vector4 _labScale = new(1f / 100, 1f / 255, 1f / 255, 1);
    private static readonly Vector4 _defaultMin = new(0f, -100f, -100f, 0f);
    private static readonly Vector4 _defaultMax = new(100f, 100f, 100f, 1f);

    private readonly FunctionColorTransform _normalizeTransform;
    private readonly ChainedColorTransform _labTransform;

    /// <summary>
    /// Initializes the space from its white point, black point, and the ranges bounding a* and b*.
    /// </summary>
    public PdfLabColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float[]? rangeArray)
    {
        Vector4 whitePointVector;
        if (whitePoint?.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
        }
        else
        {
            whitePointVector = IccTransforms.D65WhitePoint;
        }

        Vector4 labMinVector;
        Vector4 labMaxVector;

        if (rangeArray?.Length >= 4)
        {
            labMinVector = new Vector4(0f, rangeArray[0], rangeArray[2], 0);
            labMaxVector = new Vector4(100f, rangeArray[1], rangeArray[3], 1);
        }
        else
        {
            labMinVector = _defaultMin;
            labMaxVector = _defaultMax;
        }

        FunctionColorTransform normalizeTransform = new(x => (Vector4.Clamp(x, labMinVector, labMaxVector) + _labOffset) * _labScale);
        IColorTransform toXyzTransform = IccTransforms.BuildLabToXyzTransform(whitePointVector);
        IColorTransform toSrgbTransform = IccTransforms.BuildXyzToSrgbTransform(whitePointVector);

        _normalizeTransform = normalizeTransform;
        _labTransform = new ChainedColorTransform(toXyzTransform, toSrgbTransform);
    }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override IColorTransform NormalizeTransform => _normalizeTransform;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        IColorTransform? firstStage = normalize ? _normalizeTransform : null;
        ChainedColorTransform chained = new(firstStage, _labTransform, postTransform);
        return new ColorTransformSampler(chained);
    }
}
