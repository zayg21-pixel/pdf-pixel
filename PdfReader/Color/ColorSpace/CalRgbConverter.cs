using System;
using SkiaSharp;
using System.Numerics;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using PdfReader.Color.Icc.Converters;

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
    private readonly IccRgbColorConverter _iccRgb;
    private readonly bool _hasBlackPoint;
    private readonly Vector3 _blackSrgb01;

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

        float[,] m = matrix ?? new float[3, 3]
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        };

        // Bradford adaptation from CalRGB white to D50 (ICC PCS white)
        var chad = IccProfileHelpers.CreateBradfordAdaptMatrix(xw, yw, zw, 0.9642f, 1.0000f, 0.8249f);

        // Build synthetic ICC profile WITHOUT BlackPoint (see class comment)
        var profile = new IccProfile
        {
            Header = new IccProfileHeader
            {
                ColorSpace = IccConstants.SpaceRgb,
                Pcs = IccConstants.TypeXYZ,
                RenderingIntent = 1 // Relative colorimetric default
            },
            WhitePoint = new IccXyz(0.9642f, 1.0f, 0.8249f),
            BlackPoint = null, // Explicitly null: CalRGB /BlackPoint ignored for Acrobat parity
            RedMatrix = new IccXyz(m[0, 0], m[0, 1], m[0, 2]),
            GreenMatrix = new IccXyz(m[1, 0], m[1, 1], m[1, 2]),
            BlueMatrix = new IccXyz(m[2, 0], m[2, 1], m[2, 2]),
            RedTrc = IccTrc.FromGamma(gR),
            GreenTrc = IccTrc.FromGamma(gG),
            BlueTrc = IccTrc.FromGamma(gB),
            ChromaticAdaptation = chad
        };

        // Precompute adapted black point -> D50 -> sRGB01 if present.
        _hasBlackPoint = blackPoint != null && blackPoint.Length >= 3;
        if (_hasBlackPoint)
        {
            // Adapt CalGray black point to D50 then to sRGB (0..1)
            Vector3 bpXyzD50 = IccProfileHelpers.Multiply3x3(chad, blackPoint[0], blackPoint[1], blackPoint[2]);
            _blackSrgb01 = ColorMath.FromXyzD50ToSrgb01(in bpXyzD50);
        }
        else
        {
            _blackSrgb01 = Vector3.Zero;
        }

        _iccRgb = new IccRgbColorConverter(profile);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        if (!_iccRgb.TryToSrgb01(comps01, renderingIntent, out var srgb01))
        {
            return new SKColor(0, 0, 0, 255);
        }

        if (_hasBlackPoint && renderingIntent == PdfRenderingIntent.RelativeColorimetric)
        {
            Vector3 scale = Vector3.One - _blackSrgb01;
            srgb01 = _blackSrgb01 + srgb01 * scale;
        }

        byte R = ToByte(srgb01.X);
        byte G = ToByte(srgb01.Y);
        byte B = ToByte(srgb01.Z);
        return new SKColor(R, G, B, 255);
    }
}
