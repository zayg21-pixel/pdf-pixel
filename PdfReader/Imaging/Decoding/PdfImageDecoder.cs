using Microsoft.Extensions.Logging;
using PdfReader.Color.ColorSpace;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfReader.Imaging.Decoding;

/// <summary>
/// Base class for PDF image decoders.
/// </summary>
public abstract class PdfImageDecoder
{
    public PdfImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Logger = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Source PDF image to decode.
    /// </summary>
    public PdfImage Image { get; }

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
    public static PdfImageDecoder GetDecoder(PdfImage pdfImage, ILoggerFactory loggerFactory)
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
                // TODO: add JPEG2000 support
                return null;

            case PdfImageType.CCITT:
                return new CcittImageDecoder(pdfImage, loggerFactory);

            case PdfImageType.JBIG2:
                // TODO: add JBIG2 support
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the decoded image as an <see cref="SKImage"/>, or null on failure (errors are logged).
    /// </summary>
    /// <returns></returns>
    public abstract SKImage Decode();

    /// <summary>
    /// Validate image parameters and return key values needed for processing.
    /// Logs detailed errors and returns false when validation fails.
    /// </summary>
    protected bool ValidateImageParameters()
    {
        int width = Image.Width;
        int height = Image.Height;
        int bitsPerComponent = Image.BitsPerComponent;
        var converter = Image.ColorSpaceConverter;

        if (width <= 0 || height <= 0 || bitsPerComponent <= 0)
        {
            Logger.LogError("Invalid image state: Width={Width}, Height={Height}, BitsPerComponent={BitsPerComponent}.", width, height, bitsPerComponent);
            return false;
        }

        if (converter == null)
        {
            Logger.LogError("Missing color space converter for image (Name={Name}).", Image.Name);
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

        bool supportedDepth = bitsPerComponent == 1 || bitsPerComponent == 2 || bitsPerComponent == 4 || bitsPerComponent == 8 || bitsPerComponent == 16;
        if (!supportedDepth)
        {
            Logger.LogError("Unsupported BitsPerComponent value {BitsPerComponent}.", bitsPerComponent);
            return false;
        }

        return true;
    }
}
