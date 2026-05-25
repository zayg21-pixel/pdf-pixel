using PdfPixel.Color.Icc.Model;

namespace PdfPixel.Color.ColorSpace;

internal static class PdfRenderingIntentConverter
{
    public static IccRenderingIntent ToIccRenderingIntent(this PdfRenderingIntent intent)
    {
        switch (intent)
        {
            case PdfRenderingIntent.RelativeColorimetric:
                return IccRenderingIntent.RelativeColorimetric;

            case PdfRenderingIntent.AbsoluteColorimetric:
                return IccRenderingIntent.AbsoluteColorimetric;

            case PdfRenderingIntent.Perceptual:
                return IccRenderingIntent.Perceptual;

            case PdfRenderingIntent.Saturation:
                return IccRenderingIntent.Saturation;

            default:
                return IccRenderingIntent.RelativeColorimetric;
        }
    }
}
