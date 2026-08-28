using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Base class for PDF image decoders.
/// </summary>
public abstract class PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _resolvedColorSpaceConverter;

    /// <summary>
    /// Initializes the base decoder with the source image, decoding context, and logger factory.
    /// </summary>
    /// <param name="image">The PDF image descriptor to decode.</param>
    /// <param name="context">Decoding context holding the page and color space resolved for <paramref name="image"/>.</param>
    /// <param name="loggerFactory">Logger factory used to create per-decoder loggers.</param>
    protected PdfImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Logger = loggerFactory.CreateLogger(GetType());

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        Context = context;

        PdfColorSpaceConverter? converter = context.ColorSpaceConverter;
        if (converter == null)
        {
            int defaultComponents = (image.BitsPerComponent == 1) ? 1 : 3;
            converter = context.Page.Cache.ColorSpace.ResolveDeviceConverter(defaultComponents);
        }

        _resolvedColorSpaceConverter = converter ?? PdfDeviceRgbColorSpaceConverter.Instance;
    }

    /// <summary>
    /// Source PDF image to decode.
    /// </summary>
    public PdfImage Image { get; }

    /// <summary>
    /// Decoding context this decoder was constructed with, holding the page and the color space
    /// resolved for <see cref="Image"/>.
    /// </summary>
    public ImageDecodingContext Context { get; }

    /// <summary>
    /// Resolved color space converter for this image, eagerly computed during construction.
    /// Subclasses override to provide type-appropriate defaults (e.g. DeviceGray for 1-bit formats).
    /// </summary>
    protected virtual PdfColorSpaceConverter ResolvedColorSpaceConverter => _resolvedColorSpaceConverter;

    /// <summary>
    /// Logger instance for this decoder.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Logger factory instance.
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Factory: create an appropriate image decoder for the given <see cref="PdfImage"/> based on its <see cref="PdfImage.Type"/>.
    /// Returns null for unsupported encodings.
    /// </summary>
    /// <param name="pdfImage">The image descriptor to decode.</param>
    /// <param name="context">Decoding context holding the page and color space resolved for <paramref name="pdfImage"/>.</param>
    /// <param name="loggerFactory">Logger factory instance.</param>
    /// <returns>A concrete <see cref="PdfImageDecoder"/> instance, or null if unsupported.</returns>
    public static PdfImageDecoder? GetDecoder(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        if (pdfImage == null)
        {
            return null;
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        switch (pdfImage.Type)
        {
            case PdfImageType.Raw:
                return new RawImageDecoder(pdfImage, context, loggerFactory);

            case PdfImageType.JPEG:
                return new JpegImageDecoder(pdfImage, context, loggerFactory);

            case PdfImageType.JPEG2000:
                return new JpxImageDecoder(pdfImage, context, loggerFactory);

            case PdfImageType.CCITT:
                return new CcittImageDecoder(pdfImage, context, loggerFactory);

            case PdfImageType.JBIG2:
                return new Jbig2ImageDecoder(pdfImage, context, loggerFactory);

            default:
                return null;
        }
    }

    /// <summary>
    /// Prepares the decoder for a decode pass and reports the sample grid it will produce.
    /// Derived classes parse format-specific stream headers and allocate buffers here.
    /// </summary>
    /// <param name="regionsOfInterest">Regions, in original image sample coordinates, that must be reconstructed; every other region may be produced blank. Null means the whole image must be reconstructed.</param>
    /// <param name="contentLocker">Lock object used to serialize access to the compressed image data.</param>
    /// <param name="ctm">Current transformation matrix, used to compute the scaled output size.</param>
    /// <param name="observer">Observer notified during initialization steps.</param>
    /// <returns>The parameters describing the rows this decoder will produce.</returns>
    public abstract PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer);

    /// <summary>
    /// Reads the next full-width row of decoded samples into <paramref name="destination"/>, in the
    /// layout reported by <see cref="Initialize"/>. Returns false once no further row can be produced.
    /// </summary>
    /// <param name="destination">Row buffer, at least <see cref="PdfImageRowDecodingParameters.RowBytes"/> long.</param>
    /// <param name="observer">Observer notified on progress; may be null.</param>
    /// <returns>True when a row was produced; false when the decoder is exhausted.</returns>
    public abstract bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer);

    /// <summary>
    /// Builds the row decoding parameters for a decode pass, taking the entries that describe the
    /// samples from the caller and the rest from the image dictionary.
    /// </summary>
    /// <param name="ctm">Current transformation matrix, used to compute the scaled output size.</param>
    /// <param name="decodedSize">Size of the sample grid this decoder produces.</param>
    /// <param name="bitsPerComponent">Bit depth of the samples this decoder produces.</param>
    /// <param name="colorSpaceConverter">Converter resolved for the produced samples.</param>
    protected PdfImageRowDecodingParameters CreateRowDecodingParameters(
        in PdfMatrix ctm,
        in PdfIntegerSize decodedSize,
        int bitsPerComponent,
        PdfColorSpaceConverter colorSpaceConverter)
    {
        PdfIntegerSize? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, decodedSize);
        float[]? matte = ResolveSoftMaskMatte();

        return new PdfImageRowDecodingParameters(
            Context,
            decodedSize.Width,
            decodedSize.Height,
            bitsPerComponent,
            Image.RenderingIntent,
            colorSpaceConverter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.Decode,
            downscaledSize,
            ResolveSoftMaskAlphaType(matte),
            isAlphaInterleaved: false,
            matte: matte);
    }


    /// <summary>
    /// Maps the regions onto the sample grid this decoder reconstructs from, so a format-specific
    /// region only has to be given the decoder's own rectangle type. The tile grid is laid out over
    /// the size the image dictionary declares while the stream carries its own, and a tile boundary
    /// lands on the descaled grid, so every region is widened by one descaled sample on each side.
    /// </summary>
    /// <param name="regionsOfInterest">Regions in the coordinates of the image dictionary's sample grid.</param>
    /// <param name="sampleSize">Size of the stored sample grid this decoder reconstructs from.</param>
    /// <param name="descaleFactor">Power-of-two reduction the samples are reconstructed at.</param>
    protected List<PdfIntegerRectangle>? MapRegionsToSampleGrid(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfIntegerSize sampleSize,
        int descaleFactor)
    {
        if (regionsOfInterest == null)
        {
            return null;
        }

        float scaleX = (float)sampleSize.Width / Image.Width;
        float scaleY = (float)sampleSize.Height / Image.Height;

        List<PdfIntegerRectangle> mappedRegions = new(regionsOfInterest.Count);
        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            PdfIntegerRectangle region = regionsOfInterest[index];
            int left = (int)Math.Floor(region.Left * scaleX) - descaleFactor;
            int top = (int)Math.Floor(region.Top * scaleY) - descaleFactor;
            int right = (int)Math.Ceiling(region.Right * scaleX) + descaleFactor;
            int bottom = (int)Math.Ceiling(region.Bottom * scaleY) + descaleFactor;

            mappedRegions.Add(new PdfIntegerRectangle(left, top, right, bottom));
        }

        return mappedRegions;
    }

    /// <summary>
    /// Largest power-of-two reduction whose reconstruction still carries at least as many samples as the
    /// placed image needs. Samples the target size cannot show are never reconstructed in the first place.
    /// </summary>
    /// <param name="sampleSize">Size of the stored sample grid.</param>
    /// <param name="ctm">Current transformation matrix, used to compute the placed size.</param>
    /// <param name="colorSpaceConverter">Converter resolved for the decoded samples.</param>
    /// <param name="maxDescaleFactor">Largest reduction the format can reconstruct.</param>
    protected static int ComputeDescaleFactor(
        in PdfIntegerSize sampleSize,
        in PdfMatrix ctm,
        PdfColorSpaceConverter colorSpaceConverter,
        int maxDescaleFactor)
    {
        // Indexed samples are palette indices; never reconstruct them at a reduced size.
        if (colorSpaceConverter is PdfIndexedColorSpaceConverter)
        {
            return 1;
        }

        PdfIntegerSize? targetSize = PdfImageCommandUtilities.GetScaledSize(ctm, sampleSize);
        if (!targetSize.HasValue)
        {
            return 1;
        }

        int descaleFactor = 1;
        for (int candidate = 2; candidate <= maxDescaleFactor; candidate *= 2)
        {
            if (Descale(sampleSize.Width, candidate) < targetSize.Value.Width
                || Descale(sampleSize.Height, candidate) < targetSize.Value.Height)
            {
                break;
            }

            descaleFactor = candidate;
        }

        return descaleFactor;
    }

    /// <summary>
    /// Sample count left of <paramref name="sampleCount"/> after reducing by <paramref name="descaleFactor"/>.
    /// </summary>
    /// <param name="sampleCount">Stored sample count.</param>
    /// <param name="descaleFactor">Power-of-two reduction.</param>
    protected static int Descale(int sampleCount, int descaleFactor) => Math.Max(1, (sampleCount + descaleFactor - 1) / descaleFactor);

    /// <summary>
    /// Returns the alpha type contributed by the image's soft mask, or Opaque when it has none.
    /// </summary>
    /// <param name="matte">Matte components returned by <see cref="ResolveSoftMaskMatte"/>.</param>
    protected PdfImageAlphaType ResolveSoftMaskAlphaType(float[]? matte)
    {
        if (Image.SoftMask == null)
        {
            return PdfImageAlphaType.Opaque;
        }

        return (matte != null)
            ? PdfImageAlphaType.Premultiplied
            : PdfImageAlphaType.Unpremultiplied;
    }

    /// <summary>
    /// Returns the soft mask's /Matte components when they match this image's component count,
    /// or null when the mask declares none.
    /// </summary>
    protected float[]? ResolveSoftMaskMatte()
    {
        float[]? matteArray = Image.SoftMask?.MatteArray;

        if (matteArray == null || matteArray.Length != ResolvedColorSpaceConverter.Components)
        {
            return null;
        }

        return matteArray;
    }

    /// <summary>
    /// Validate image parameters and return key values needed for processing.
    /// Logs detailed errors and returns false when validation fails.
    /// </summary>
    protected bool ValidateImageParameters()
    {
        int width = Image.Width;
        int height = Image.Height;
        int bitsPerComponent = Image.BitsPerComponent;
        PdfColorSpaceConverter converter = ResolvedColorSpaceConverter;

        if (width <= 0 || height <= 0 || bitsPerComponent <= 0)
        {
            Logger.LogError("Invalid image state: Width={Width}, Height={Height}, BitsPerComponent={BitsPerComponent}.", width, height, bitsPerComponent);
            return false;
        }

        if (Image.HasImageMask && bitsPerComponent != 1)
        {
            Logger.LogError("/ImageMask requires BitsPerComponent=1 (actual={BitsPerComponent}).", bitsPerComponent);
            return false;
        }

        if (converter is PdfIndexedColorSpaceConverter && bitsPerComponent == 16)
        {
            Logger.LogError("Indexed color space does not support 16 bits per component.");
            return false;
        }

        if (bitsPerComponent < 1 || bitsPerComponent > 16)
        {
            Logger.LogError("Unsupported BitsPerComponent value {BitsPerComponent}.", bitsPerComponent);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Releases all transient state accumulated during a single decode pass.
    /// Safe to call multiple times; does not affect the lifetime of the decoder itself.
    /// </summary>
    public virtual void Cleanup()
    {
    }
}
