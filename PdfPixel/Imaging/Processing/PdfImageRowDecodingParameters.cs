using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Model;
using SkiaSharp;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Describes the image sample properties required by <see cref="PdfImageRowProcessor"/>
/// for a single decode pass. Callers populate this from the source <see cref="PdfImage"/>
/// and apply any format-specific overrides (e.g. component count or bit depth derived from
/// a JPEG or JPX stream header) before constructing the row processor.
/// </summary>
public sealed class PdfImageRowDecodingParameters
{
    public PdfImageRowDecodingParameters(
        ImageDecodingContext context,
        int width,
        int height,
        int bitsPerComponent,
        PdfRenderingIntent renderingIntent,
        PdfColorSpaceConverter colorSpaceConverter,
        bool hasImageMask,
        int[] maskArray,
        float[] decodeArray,
        SKSizeI? downscaledSize,
        int descaleFactor)
    {
        Width = width;
        Height = height;
        BitsPerComponent = bitsPerComponent;
        RenderingIntent = renderingIntent;
        ColorSpaceConverter = colorSpaceConverter;
        Context = context;
        HasImageMask = hasImageMask;
        MaskArray = maskArray;
        DecodeArray = decodeArray;
        DownscaledSize = downscaledSize;
        DescaleFactor = descaleFactor;
    }

    public int Width { get; }
    public int Height { get; }
    public int BitsPerComponent { get; }
    public PdfRenderingIntent RenderingIntent { get; }
    public PdfColorSpaceConverter ColorSpaceConverter { get; }
    public bool HasImageMask { get; }
    public int[] MaskArray { get; }
    public float[] DecodeArray { get; }
    public SKSizeI? DownscaledSize { get; }

    public int DescaleFactor { get; }
    public ImageDecodingContext Context { get; }

    /// <summary>
    /// Returns the downscaled output size for the given source dimensions and decoding context,
    /// or null when downscaling is not applicable (indexed color space, Type 3 rendering, or
    /// the context reports no size reduction).
    /// </summary>
    public static SKSizeI? ComputeDownscaledSize(int width, int height, PdfColorSpaceConverter colorSpaceConverter, ImageDecodingContext context, SKMatrix ctm)
    {
        if (colorSpaceConverter is IndexedConverter || context.IsType3Rendering)
        {
            return null;
        }

        return PdfImageCommandUtilities.GetScaledSize(ctm, new SKSizeI(width, height));
    }

    public static PdfImageRowDecodingParameters FromImage(PdfImage image, ImageDecodingContext context, SKMatrix ctm)
    {
        var downscaledSize = ComputeDownscaledSize(image.Width, image.Height, image.ColorSpaceConverter, context, ctm);

        return new(context, image.Width, image.Height, image.BitsPerComponent,
            image.RenderingIntent, image.ColorSpaceConverter,
            image.HasImageMask, image.MaskArray, image.DecodeArray,
            downscaledSize, descaleFactor: 1);
    }
}
