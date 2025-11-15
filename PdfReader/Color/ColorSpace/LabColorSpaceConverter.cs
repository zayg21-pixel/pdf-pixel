using System;
using SkiaSharp;
using System.Numerics;
using PdfReader.Color.Icc.Utilities;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for PDF /Lab (CIEBasedLab) color space (NOT ICCBased).
/// </summary>
internal sealed class LabColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly bool _needsAdapt;
    private readonly Vector3 _adaptRow0;    // Precomputed Bradford rows for SIMD dot usage
    private readonly Vector3 _adaptRow1;
    private readonly Vector3 _adaptRow2;
    private readonly Vector3 _whiteScale;   // (Xw/0.9642, Yw/1, Zw/0.8249)
    private readonly Vector3 _labMin;       // (0, aMin, bMin)
    private readonly Vector3 _labMax;       // (100, aMax, bMax)

    public LabColorSpaceConverter(float[] whitePoint, float[] rangeArray)
    {
        // Guard inputs (use D50 if malformed)
        if (whitePoint == null || whitePoint.Length < 3)
        {
            whitePoint = [0.9642f, 1.0f, 0.8249f];
        }

        float lxw = whitePoint[0] <= 0f ? 0.9642f : whitePoint[0];
        float lyw = whitePoint[1] <= 0f ? 1.0f : whitePoint[1];
        float lzw = whitePoint[2] <= 0f ? 0.8249f : whitePoint[2];

        float aMin = -100f;
        float aMax = 100f;
        float bMin = -100f;
        float bMax = 100f;

        if (rangeArray != null && rangeArray.Length >= 4)
        {
            aMin = rangeArray[0];
            aMax = rangeArray[1];
            bMin = rangeArray[2];
            bMax = rangeArray[3];
        }

        _whiteScale = new Vector3(lxw / 0.9642f, lyw / 1.0f, lzw / 0.8249f);
        _labMin = new Vector3(0f, aMin, bMin);
        _labMax = new Vector3(100f, aMax, bMax);

        const float eps = 1e-3f;
        if (Math.Abs(lxw - 0.9642f) < eps && Math.Abs(lyw - 1.0f) < eps && Math.Abs(lzw - 0.8249f) < eps)
        {
            _needsAdapt = false;
        }
        else
        {
            _needsAdapt = true;
            var m = IccProfileHelpers.CreateBradfordAdaptMatrix(lxw, lyw, lzw, 0.9642f, 1.0f, 0.8249f);
            _adaptRow0 = new Vector3(m[0,0], m[0,1], m[0,2]);
            _adaptRow1 = new Vector3(m[1,0], m[1,1], m[1,2]);
            _adaptRow2 = new Vector3(m[2,0], m[2,1], m[2,2]);
        }
    }

    public override int Components => 3;
    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps, PdfRenderingIntent intent)
    {
        float Lraw = comps.Length > 0 ? comps[0] : 0f;
        float araw = comps.Length > 1 ? comps[1] : 0f;
        float braw = comps.Length > 2 ? comps[2] : 0f;

        Vector3 lab = Vector3.Clamp(new Vector3(Lraw, araw, braw), _labMin, _labMax);

        Vector3 xyzD50Base = ColorMath.LabD50ToXyz(lab.X, lab.Y, lab.Z);
        Vector3 xyz = xyzD50Base * _whiteScale;
        if (_needsAdapt)
        {
            xyz = new Vector3(Vector3.Dot(_adaptRow0, xyz),
                               Vector3.Dot(_adaptRow1, xyz),
                               Vector3.Dot(_adaptRow2, xyz));
        }

        var srgb01 = ColorMath.FromXyzD50ToSrgb01(in xyz);
        byte R = ToByte(srgb01.X);
        byte G = ToByte(srgb01.Y);
        byte B = ToByte(srgb01.Z);
        return new SKColor(R, G, B);
    }
}
