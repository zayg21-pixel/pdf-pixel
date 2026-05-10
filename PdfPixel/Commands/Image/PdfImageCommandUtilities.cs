using PdfPixel.Color.Paint;
using PdfPixel.Imaging.Model;
using SkiaSharp;

namespace PdfPixel.Commands.Image;

internal static class PdfImageCommandUtilities
{
    /// <summary>
    /// Returns the matrix that maps PDF image space to Skia canvas space.
    /// Equivalent to: <c>canvas.Concat(Scale(1,−1))</c> then <c>canvas.Concat(Translate(0,−1))</c>.
    /// Emit via <see cref="ConcatMatrixCommand"/> instead of calling canvas directly.
    /// </summary>
    public static SKMatrix GetImageMatrix()
        => SKMatrix.Concat(SKMatrix.CreateScale(1, -1), SKMatrix.CreateTranslation(0, -1));

    /// <summary>
    /// Creates a paint that draws <paramref name="shader"/> with the blend mode and fill
    /// alpha captured in <paramref name="context"/>.
    /// </summary>
    public static SKPaint GetBaseImagePaint(SKShader shader, ImageDecodingContext context)
    {
        return new SKPaint
        {
            Shader = shader,
            BlendMode = context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, context.FillAlpha),
        };
    }

    /// <summary>
    /// Returns the appropriate sampling options for <paramref name="pdfImage"/> given the
    /// current decoding context.  Use <see cref="SKFilterMode.Nearest"/> for stencil masks
    /// regardless — they are always 1-bit and interpolation would corrupt the alpha shape.
    /// </summary>
    public static SKSamplingOptions GetSamplingOptions(ImageDecodingContext context, PdfImage pdfImage)
    {
        bool isDownscaled = context.GetScaledSize(new SKSizeI(pdfImage.Width, pdfImage.Height)).HasValue;

        if (isDownscaled || context.IsType3Rendering || pdfImage.Interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        }

        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    // TODO: add method similar to SKPath drawing to check if AA is allowed
}
