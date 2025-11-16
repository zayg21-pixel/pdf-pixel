using System;
using SkiaSharp;
using System.Numerics;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for CalRGB (CIEBasedABC) color space.
/// Builds a synthetic ICC RGB (matrix/TRC + chromatic adaptation) profile
/// from CalRGB parameters and delegates to IccRgbColorConverter.
/// PDF BlackPoint is applied after ICC RGB conversion in sRGB space by
/// interpolating between the adapted black point and the ICC-derived neutral.
/// </summary>
internal sealed class CalRgbConverter : PdfColorSpaceConverter
{
    private readonly bool _hasBlackPoint;
    private readonly Vector3 _pcsRow0, _pcsRow1, _pcsRow2;
    private readonly IccTrc _rTrc, _gTrc, _bTrc;
    private readonly Vector3 _blackPointXyz;

    public CalRgbConverter(float[] whitePoint, float[] blackPoint, float[] gamma, float[,] matrix)
    {
        float xw = 0.9505f;
        float yw = 1.0f;
        float zw = 1.0890f;

        if (whitePoint != null && whitePoint.Length == 3)
        {
            if (whitePoint[0] > 0f) xw = whitePoint[0];
            if (whitePoint[1] > 0f) yw = whitePoint[1];
            if (whitePoint[2] > 0f) zw = whitePoint[2];
        }

        if (gamma == null || gamma.Length < 3)
        {
            gamma = [1.0f, 1.0f, 1.0f];
        }

        float gR = gamma[0] <= 0 ? 1.0f : gamma[0];
        float gG = gamma[1] <= 0 ? 1.0f : gamma[1];
        float gB = gamma[2] <= 0 ? 1.0f : gamma[2];

        matrix ??= new float[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            };

        // Bradford adaptation from CalRGB white to D50 (ICC PCS white)
        var chad = IccProfileHelpers.CreateBradfordAdaptMatrix(xw, yw, zw, 0.9642f, 1.0000f, 0.8249f);

        var adaptedPimaries = IccProfileHelpers.Multiply3x3(chad, matrix);
        (_pcsRow0, _pcsRow1, _pcsRow2) = IccProfileHelpers.ToVectorRows(adaptedPimaries);
        _rTrc = IccTrc.FromGamma(gR);
        _gTrc = IccTrc.FromGamma(gG);
        _bTrc = IccTrc.FromGamma(gB);

        // Precompute adapted black point -> D50
        _hasBlackPoint = blackPoint != null && blackPoint.Length >= 3;
        if (_hasBlackPoint)
        {
            _blackPointXyz = IccProfileHelpers.Multiply3x3(chad, blackPoint[0], blackPoint[1], blackPoint[2]);
        }
        else
        {
            _blackPointXyz = Vector3.Zero;
        }
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        var c0 = comps01.Length > 0 ? comps01[0] : 0f;
        var c1 = comps01.Length > 1 ? comps01[1] : 0f;
        var c2 = comps01.Length > 2 ? comps01[2] : 0f;

        float rLinear = IccTrcEvaluator.EvaluateTrc(_rTrc, c0);
        float gLinear = IccTrcEvaluator.EvaluateTrc(_gTrc, c1);
        float bLinear = IccTrcEvaluator.EvaluateTrc(_bTrc, c2);
        Vector3 linear = new Vector3(rLinear, gLinear, bLinear);

        float X = Vector3.Dot(_pcsRow0, linear);
        float Y = Vector3.Dot(_pcsRow1, linear);
        float Z = Vector3.Dot(_pcsRow2, linear);
        Vector3 xyz = new Vector3(X, Y, Z);

        if (_hasBlackPoint && renderingIntent == PdfRenderingIntent.RelativeColorimetric)
        {
            // TODO: this applied incorrectly
            Vector3 scale = Vector3.One - _blackPointXyz;
            xyz = _blackPointXyz + xyz * scale;
        }

        Vector3 srgb01 = ColorMath.FromXyzD50ToSrgb01(xyz);

        byte R = ToByte(srgb01.X);
        byte G = ToByte(srgb01.Y);
        byte B = ToByte(srgb01.Z);
        return new SKColor(R, G, B, 255);
    }
}
