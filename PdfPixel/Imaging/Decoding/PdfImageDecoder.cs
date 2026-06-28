using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Base class for PDF image decoders.
/// </summary>
public abstract class PdfImageDecoder : IDisposable
{
    /// <summary>
    /// Initializes the base decoder with the source image and logger factory.
    /// </summary>
    /// <param name="image">The PDF image descriptor to decode.</param>
    /// <param name="loggerFactory">Logger factory used to create per-decoder loggers.</param>
    private readonly PdfColorSpaceConverter _resolvedColorSpaceConverter;

    protected PdfImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Logger = loggerFactory.CreateLogger(GetType());

        PdfColorSpaceConverter? converter = image.ColorSpaceConverter;
        if (converter == null)
        {
            int defaultComponents = (image.BitsPerComponent == 1) ? 1 : 3;
            converter = image.Page.Cache.ColorSpace.ResolveDeviceConverter(defaultComponents);
        }

        _resolvedColorSpaceConverter = converter ?? DeviceRgbConverter.Instance;
    }

    /// <summary>
    /// Source PDF image to decode.
    /// </summary>
    public PdfImage Image { get; }

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
    /// <param name="loggerFactory">Logger factory instance.</param>
    /// <returns>A concrete <see cref="PdfImageDecoder"/> instance, or null if unsupported.</returns>
    public static PdfImageDecoder? GetDecoder(PdfImage pdfImage, ILoggerFactory loggerFactory)
    {
        if (pdfImage == null)
        {
            return null;
        }

        switch (pdfImage.Type)
        {
            case PdfImageType.Raw:
                return new RawImageDecoder(pdfImage, loggerFactory);

            case PdfImageType.JPEG:
                return new JpegImageDecoder(pdfImage, loggerFactory);

            case PdfImageType.JPEG2000:
                return new JpxImageDecoder(pdfImage, loggerFactory);

            case PdfImageType.CCITT:
                return new CcittImageDecoder(pdfImage, loggerFactory);

            case PdfImageType.JBIG2:
                return new Jbig2ImageDecoder(pdfImage, loggerFactory);

            default:
                return null;
        }
    }

    /// <summary>
    /// Prepares the decoder for a decode pass over the given tile grid and region of interest.
    /// Derived classes override this to parse format-specific stream headers and allocate buffers.
    /// </summary>
    /// <param name="tileInfo">Tile grid dimensions for this decode pass.</param>
    /// <param name="context">Rendering context carrying target surface and quality settings.</param>
    /// <param name="contentLocker">Lock object used to serialize access to the compressed image data.</param>
    /// <param name="ctm">Current transformation matrix, used to compute the scaled output size.</param>
    /// <param name="tileIndexesToDecode">Indexes of tiles that must be decoded; every other tile is produced as a skipped placeholder. Null means every tile must be decoded.</param>
    /// <param name="observer">Observer notified during initialization steps.</param>
    public virtual void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver observer)
    {
    }

    /// <summary>
    /// Decodes the next batch of image rows and returns any tiles completed during this call.
    /// Returns null when all tiles have been produced.
    /// </summary>
    /// <param name="observer">Observer notified on progress; may be null.</param>
    /// <returns>Completed <see cref="PdfImageTile"/> instances, or null when decoding is finished.</returns>
    public virtual PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer) => null;

    /// <summary>
    /// Validate image parameters and return key values needed for processing.
    /// Logs detailed errors and returns false when validation fails.
    /// </summary>
    protected bool ValidateImageParameters()
    {
        int width = Image.Width;
        int height = Image.Height;
        int bitsPerComponent = Image.BitsPerComponent;
        PdfColorSpaceConverter? converter = Image.ColorSpaceConverter;

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

        if (converter is IndexedConverter && bitsPerComponent == 16)
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

    /// <inheritdoc/>
    protected abstract void Dispose(bool disposing);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
