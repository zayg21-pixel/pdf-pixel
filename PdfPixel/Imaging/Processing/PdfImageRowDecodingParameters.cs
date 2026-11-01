using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Imaging.Decoding;
using System;
using PdfPixel.Geometry;
using PdfPixel.Objects;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Describes the image sample properties required by <see cref="Pipeline.IPdfImagePipelineFactory"/>
/// for a single decode pass. Callers populate this from the source <see cref="PdfImage"/>
/// and apply any format-specific overrides (e.g. component count or bit depth derived from
/// a JPEG or JPX stream header) before creating the pipelines.
/// </summary>
public sealed class PdfImageRowDecodingParameters
{
    internal PdfImageRowDecodingParameters(
        PdfPaint imagePaint,
        int width,
        int height,
        int bitsPerComponent,
        bool hasImageMask,
        int[]? colorKeyMask,
        PdfRange[]? decode,
        PdfIntegerSize? downscaledSize,
        PdfImageAlphaType alphaType = PdfImageAlphaType.Opaque,
        bool isAlphaInterleaved = false,
        float[]? matte = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Sample width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Sample height must be positive.");
        }

        if (bitsPerComponent < 1 || bitsPerComponent > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Sample bit depth must be between 1 and 16.");
        }

        if (hasImageMask && bitsPerComponent != 1)
        {
            throw new ArgumentException("A stencil mask requires a sample bit depth of 1.", nameof(bitsPerComponent));
        }

        if (imagePaint == null)
        {
            throw new ArgumentNullException(nameof(imagePaint));
        }

        if (!hasImageMask && imagePaint.ColorSpace is PdfIndexedColorSpaceConverter && bitsPerComponent == 16)
        {
            throw new ArgumentException("An indexed color space does not support a sample bit depth of 16.", nameof(imagePaint));
        }

        if ((alphaType == PdfImageAlphaType.Premultiplied) != (matte != null))
        {
            throw new ArgumentException("A premultiplied alpha type requires matte components, and no other type accepts them.", nameof(matte));
        }

        Width = width;
        Height = height;
        BitsPerComponent = bitsPerComponent;
        ImagePaint = imagePaint;
        HasImageMask = hasImageMask;
        ColorKeyMask = colorKeyMask;
        Decode = decode;
        DownscaledSize = downscaledSize;
        AlphaType = alphaType;
        IsAlphaInterleaved = isAlphaInterleaved;
        Matte = matte;
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
    /// Paint the image is drawn with: the fill paint with the image's color space and color conversion, or the fill paint
    /// unchanged for stencil masks.
    /// </summary>
    public PdfPaint ImagePaint { get; }

    /// <summary>
    /// True when the image is a stencil mask (PDF /ImageMask).
    /// </summary>
    public bool HasImageMask { get; }

    /// <summary>
    /// Color-key masking range pairs from the PDF /Mask entry, or null when not specified.
    /// </summary>
    public int[]? ColorKeyMask { get; }

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
    /// Backdrop components from the soft mask's /Matte entry, set when <see cref="AlphaType"/> is Premultiplied.
    /// </summary>
    public float[]? Matte { get; }

    /// <summary>
    /// True when alpha arrives as a separate 8-bit plane alongside each row.
    /// </summary>
    public bool HasAlphaPlane => AlphaType != PdfImageAlphaType.Opaque && !IsAlphaInterleaved;

    /// <summary>
    /// Number of components in each packed sample, counting interleaved alpha.
    /// </summary>
    public int ComponentCount => (HasImageMask ? 1 : ImagePaint.ColorSpace.Components) + (IsAlphaInterleaved ? 1 : 0);

    /// <summary>
    /// Size in bytes of one full-width row of packed samples.
    /// </summary>
    public int RowBytes => ((Width * ComponentCount * BitsPerComponent) + 7) / 8;
}
