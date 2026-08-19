using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Describes the image sample properties required by <see cref="PdfImageRowProcessor"/>
/// for a single decode pass. Callers populate this from the source <see cref="PdfImage"/>
/// and apply any format-specific overrides (e.g. component count or bit depth derived from
/// a JPEG or JPX stream header) before constructing the row processor.
/// </summary>
public sealed class PdfImageRowDecodingParameters
{
    /// <summary>
    /// Initializes a complete set of row-decoding parameters.
    /// </summary>
    /// <param name="context">Rendering context for the current decode pass.</param>
    /// <param name="width">Width of the source image in samples.</param>
    /// <param name="height">Height of the source image in samples.</param>
    /// <param name="bitsPerComponent">Bit depth of each color component in the source stream.</param>
    /// <param name="renderingIntent">PDF rendering intent applied during color conversion.</param>
    /// <param name="colorSpaceConverter">Converter that maps raw sample values to output colors.</param>
    /// <param name="hasImageMask">True when the image is a stencil mask (PDF /ImageMask).</param>
    /// <param name="maskArray">Color-key masking range pairs from the PDF /Mask entry, or null.</param>
    /// <param name="decode">Sample remapping table from the PDF /Decode entry, or null for the default.</param>
    /// <param name="downscaledSize">Target output size after downscaling, or null when no downscaling is applied.</param>
    /// <param name="alphaType">How the alpha accompanying the color samples relates to them, or Opaque when there is none.</param>
    /// <param name="isAlphaInterleaved">True when alpha is the last component of each packed sample, false when it arrives as a separate 8-bit plane.</param>
    /// <param name="premultipliedBackdrop">Backdrop color the samples were blended with, required when <paramref name="alphaType"/> is Premultiplied and null otherwise.</param>
    public PdfImageRowDecodingParameters(
        ImageDecodingContext context,
        int width,
        int height,
        int bitsPerComponent,
        PdfRenderingIntent renderingIntent,
        PdfColorSpaceConverter colorSpaceConverter,
        bool hasImageMask,
        int[]? maskArray,
        PdfRange[]? decode,
        PdfIntegerSize? downscaledSize,
        PdfImageAlphaType alphaType = PdfImageAlphaType.Opaque,
        bool isAlphaInterleaved = false)
    {
        Width = width;
        Height = height;
        BitsPerComponent = bitsPerComponent;
        RenderingIntent = renderingIntent;
        ColorSpaceConverter = colorSpaceConverter;
        Context = context;
        HasImageMask = hasImageMask;
        MaskArray = maskArray;
        Decode = decode;
        DownscaledSize = downscaledSize;
        AlphaType = alphaType;
        IsAlphaInterleaved = isAlphaInterleaved;
    }

    /// <summary>
    /// Width of the source image in samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the source image in samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Bit depth of each color component in the source stream.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// PDF rendering intent applied during color conversion.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Converter that maps raw sample values to output colors.
    /// </summary>
    public PdfColorSpaceConverter ColorSpaceConverter { get; }

    /// <summary>
    /// True when the image is a stencil mask (PDF /ImageMask).
    /// </summary>
    public bool HasImageMask { get; }

    /// <summary>
    /// Color-key masking range pairs from the PDF /Mask entry, or null when not specified.
    /// </summary>
    public int[]? MaskArray { get; }

    /// <summary>
    /// Sample remapping table from the PDF /Decode entry, or null for the default mapping.
    /// </summary>
    public PdfRange[]? Decode { get; }

    /// <summary>
    /// Target output size after downscaling, or null when no downscaling is applied.
    /// </summary>
    public PdfIntegerSize? DownscaledSize { get; }

    /// <summary>
    /// How the alpha accompanying the color samples relates to them, or Opaque when there is none.
    /// </summary>
    public PdfImageAlphaType AlphaType { get; }

    /// <summary>
    /// True when alpha is the last component of each packed sample, false when it arrives as a separate 8-bit plane.
    /// </summary>
    public bool IsAlphaInterleaved { get; }

    /// <summary>
    /// True when alpha arrives as a separate 8-bit plane alongside each row.
    /// </summary>
    public bool HasAlphaPlane => AlphaType != PdfImageAlphaType.Opaque && !IsAlphaInterleaved;

    /// <summary>
    /// Number of components in each packed sample, counting interleaved alpha.
    /// </summary>
    public int ComponentCount => ColorSpaceConverter.Components + ((IsAlphaInterleaved) ? 1 : 0);

    /// <summary>
    /// Size in bytes of one full-width row of packed samples.
    /// </summary>
    public int RowBytes => ((Width * ComponentCount * BitsPerComponent) + 7) / 8;

    /// <summary>
    /// Rendering context for the current decode pass.
    /// </summary>
    public ImageDecodingContext Context { get; }
}
