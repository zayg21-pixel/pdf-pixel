using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// <see cref="CalRgbConverter"/> based converter for CalGray (CIEBasedGray) color space.
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        float g01 = comps01.Length > 0 ? comps01[0] : 0f;
        return _sourceCalRgb.ToSrgb([g01, g01, g01], renderingIntent);
    }
}
