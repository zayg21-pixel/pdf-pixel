using System;
using SkiaSharp;
using System.Numerics;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Utilities;
using PdfReader.Color.Icc.Converters;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for CalGray (CIEBasedA) color space.
/// Uses a synthetic ICC Gray profile (white + TRC + chromatic adaptation) but
/// does NOT embed the CalGray BlackPoint in the profile. Instead the explicit
/// PDF BlackPoint is applied after ICC grayscale conversion in sRGB space by
/// interpolating between the adapted black point and the ICC-derived neutral.
/// </summary>
internal sealed class CalGrayConverter : PdfColorSpaceConverter
{
    private readonly IccGrayColorConverter _iccGray;
    private readonly Vector3 _blackSrgb01;
    private readonly bool _hasBlackPoint;

    public CalGrayConverter(float[] whitePoint, float[] blackPoint, float? gamma)
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

        // Synthetic ICC Gray profile: white point + TRC + chromatic adaptation (CalGray WP -> D50).
        // Do NOT set BlackPoint; we will apply PDF BlackPoint semantics manually afterwards.
        var chad = IccProfileHelpers.CreateBradfordAdaptMatrix(xw, yw, zw, 0.9642f, 1.0000f, 0.8249f);
        var profile = new IccProfile
        {
            Header = new IccProfileHeader
            {
                ColorSpace = IccConstants.SpaceGray,
                Pcs = IccConstants.TypeXYZ,
                RenderingIntent = 1
            },
            WhitePoint = new IccXyz(0.9642f, 1.0000f, 0.8249f),
            GrayTrc = IccTrc.FromGamma(gamma ?? 1.0f),
            ChromaticAdaptation = chad
        };

        _iccGray = new IccGrayColorConverter(profile);

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
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        float g01 = comps01.Length > 0 ? comps01[0] : 0f;
        if (g01 < 0f)
        {
            g01 = 0f;
        }
        else if (g01 > 1f)
        {
            g01 = 1f;
        }

        if (!_iccGray.TryToSrgb01(g01, renderingIntent, out var rgb01))
        {
            return new SKColor(0, 0, 0, 255);
        }

        // Vectorized black point compensation (relative colorimetric only):
        // rgb' = black + (rgb * (1 - black)) per component using Vector3 math.
        if (_hasBlackPoint && renderingIntent == PdfRenderingIntent.RelativeColorimetric)
        {
            Vector3 scale = Vector3.One - _blackSrgb01;
            rgb01 = _blackSrgb01 + rgb01 * scale;
        }

        byte R = ToByte(rgb01.X);
        byte G = ToByte(rgb01.Y);
        byte B = ToByte(rgb01.Z);
        return new SKColor(R, G, B, 255);
    }
}
