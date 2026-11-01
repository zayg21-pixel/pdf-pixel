using System;
using System.Numerics;
using PdfPixel.Color.Transform;
using PdfPixel.Color.Icc;
using PdfPixel.Objects;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Lab color space: a lightness component and two chromatic components, a* and b*, in the
/// CIE 1976 L*a*b* space.
/// </summary>
public sealed class PdfLabColorSpaceConverter : PdfColorSpaceConverter
{
    private static readonly Vector4 _labOffset = new(0, 128, 128, 0);
    private static readonly Vector4 _labScale = new(1f / 100, 1f / 255, 1f / 255, 1);

    private readonly FunctionColorTransform _normalizeTransform;
    private readonly FunctionColorTransform _rangeTransform;
    private readonly ChainedColorTransform _labTransform;
    private readonly ChainedColorTransform _absoluteLabTransform;
    private readonly ChainedColorTransform _toXyzTransform;
    private readonly ChainedColorTransform _absoluteToXyzTransform;

    internal PdfLabColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float[]? range)
    {
        if (whitePoint?.Length >= 3)
        {
            WhitePoint = whitePoint.AsSpan(0, 3).ToArray();
        }
        else
        {
            Vector4 d65 = IccTransforms.D65WhitePoint;
            WhitePoint = new float[] { d65.X, d65.Y, d65.Z };
        }

        if (blackPoint?.Length >= 3)
        {
            BlackPoint = blackPoint.AsSpan(0, 3).ToArray();
        }
        else
        {
            BlackPoint = new float[] { 0f, 0f, 0f };
        }

        if (range?.Length >= 4)
        {
            Range = [new PdfRange(range[0], range[1]), new PdfRange(range[2], range[3])];
        }
        else
        {
            Range = [new PdfRange(-100f, 100f), new PdfRange(-100f, 100f)];
        }

        ComponentRanges = new PdfRange[] { new(0f, 100f), Range[0], Range[1] };

        Vector4 whitePointVector = new(WhitePoint[0], WhitePoint[1], WhitePoint[2], 1f);
        Vector4 labMinVector = new(0f, Range[0].Min, Range[1].Min, 0f);
        Vector4 labMaxVector = new(100f, Range[0].Max, Range[1].Max, 1f);

        FunctionColorTransform normalizeTransform = new(x => (Vector4.Clamp(x, labMinVector, labMaxVector) + _labOffset) * _labScale);

        Vector4 labSpanVector = labMaxVector - labMinVector;
        _rangeTransform = new FunctionColorTransform(x => (labMinVector + (x * labSpanVector) + _labOffset) * _labScale);
        _normalizeTransform = normalizeTransform;

        // WhitePoint is the ICC media white: media-relative colorimetry scales XYZ by D50 / WhitePoint,
        // which equals reading the values as D50 Lab; absolute colorimetry keeps the unscaled XYZ.
        // BlackPoint is not applied: L* 0 stays black under every rendering intent.
        IColorTransform absoluteLabToXyzTransform = IccTransforms.BuildLabToXyzTransform(whitePointVector);
        _labTransform = new ChainedColorTransform(IccTransforms.LabD50ToXyzTransform, IccTransforms.XyzD50ToSrgbTransform);
        _absoluteLabTransform = new ChainedColorTransform(absoluteLabToXyzTransform, IccTransforms.XyzD50ToSrgbTransform);
        _toXyzTransform = new ChainedColorTransform(normalizeTransform, IccTransforms.LabD50ToXyzTransform);
        _absoluteToXyzTransform = new ChainedColorTransform(normalizeTransform, absoluteLabToXyzTransform);
    }

    /// <summary>
    /// Gets the diffuse white point in CIE XYZ (/WhitePoint), or D65 when absent.
    /// </summary>
    public float[] WhitePoint { get; }

    /// <summary>
    /// Gets the diffuse black point in CIE XYZ (/BlackPoint), or [0 0 0] when absent.
    /// </summary>
    public float[] BlackPoint { get; }

    /// <summary>
    /// Gets the valid ranges of a* and b* (/Range), or [-100 100] for each when absent.
    /// </summary>
    public PdfRange[] Range { get; }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override IColorTransform NormalizeTransform => _normalizeTransform;

    /// <inheritdoc />
    public override PdfRange[]? ComponentRanges { get; }

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
    {
        FunctionColorTransform firstStage = _rangeTransform;
        if (normalize)
        {
            firstStage = _normalizeTransform;
        }

        ChainedColorTransform labTransform = _labTransform;
        if (parameters.RenderingIntent == PdfRenderingIntent.AbsoluteColorimetric)
        {
            labTransform = _absoluteLabTransform;
        }

        ChainedColorTransform chained = new(firstStage, labTransform, parameters.TransferFunction);
        return new ColorTransformSampler(chained);
    }

    /// <inheritdoc />
    public override ChainedColorTransform GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
    {
        if (renderingIntent == PdfRenderingIntent.AbsoluteColorimetric)
        {
            return _absoluteToXyzTransform;
        }

        return _toXyzTransform;
    }
}
