using System;
using SkiaSharp;

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
    private readonly CalRgbConverter _sourceCalRgb;

    public CalGrayConverter(float[] whitePoint, float[] blackPoint, float? gamma)
    {
        _sourceCalRgb = new CalRgbConverter(whitePoint,
            blackPoint,
            gamma.HasValue ? [gamma.Value, gamma.Value, gamma.Value] : null,
            null);
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        float g01 = comps01.Length > 0 ? comps01[0] : 0f;
        return _sourceCalRgb.ToSrgb([g01, g01, g01], renderingIntent);
    }
}
