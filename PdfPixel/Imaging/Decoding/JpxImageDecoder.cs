using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Provides functionality for decoding images in the JPEG 2000 (JPX) format.
/// Supports both header parsing and full decoding with tile-to-row conversion.
/// </summary>
/// <remarks>
/// Use this class to read and decode JPX image files, which are commonly used for high-quality image
/// storage and transmission. This class is typically used in applications that require support for the JPEG 2000
/// standard.
/// </remarks>
public class JpxImageDecoder : PdfImageDecoder
{
    public JpxImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override SKImage Decode(
        ImageDecodingContext context,
        CancellationToken cancellationToken)
    {
        if (!ValidateImageParameters())
        {
            return null;
        }
        // TODO: [MEDIUM] process alpha correctly

        ReadOnlyMemory<byte> encodedImageData = Image.GetImageData();

        if (encodedImageData.Length == 0)
        {
            Logger.LogError("JPX image data is empty (Name={Name}).", Image.Name);
            return null;
        }

        // Parse JPX header
        JpxHeader header = JpxReader.ParseHeader(encodedImageData.Span);

        if (header == null)
        {
            throw new InvalidOperationException($"JPX header invalid or missing (Image={Image.Name}).");
        }

        if (header.Width == 0 || header.Height == 0)
        {
            throw new InvalidOperationException($"Invalid JPX dimensions Width={header.Width} Height={header.Height} (Image={Image.Name}).");
        }

        if (header.ComponentCount == 0)
        {
            throw new InvalidOperationException($"Invalid JPX component count {header.ComponentCount} (Image={Image.Name}).");
        }

        // Detect JPX-native opacity channel declared via the cdef box.
        // Per ISO 32000-1 §8.9.5.4, when the JPX stream declares an opacity component
        // it overrides any external SMask in the image dictionary.
        // A single-component image whose sole cdef entry is type 1 or 2 carries only alpha
        // data; its decoded pixel values map directly to opacity [0..max] with no colour.
        bool isOpacityOnlyStream =
            header.HasOpacityChannel &&
            header.OpacityComponentIndex == 0 &&
            header.ComponentCount == 1; // TODO: use.

        // Compute JPX-native descale factor from target rendering size
        var decodingParameters = ComputeDecodingParameters(header, context);

        // Decode codestream into row provider
        ReadOnlySpan<byte> codestreamData = encodedImageData.Span.Slice(header.CodestreamOffset);
        var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
        var tileProvider = new JpxTileProvider(header, codestreamData, tileDecoder, decodingParameters);

        using var rowProvider = new JpxTileToRowConverter(header, tileProvider, decodingParameters);

        // The row provider emits at the reduced resolution; tell the row processor
        // so it can further downscale only the remainder (if any).
        SKSizeI? inputSizeOverride = decodingParameters.DescaleFactor > 1
            ? new SKSizeI(rowProvider.Width, rowProvider.Height)
            : null;
        // Stream decoded data through PdfImageRowProcessor
        PdfImageRowProcessor rowProcessor = null;

        try
        {
            rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>(), context, inputSizeOverride);
            rowProcessor.InitializeBuffer();

            byte[] rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];

            for (int rowIndex = 0; rowIndex < rowProvider.Height; rowIndex++)
            {
                if (!rowProvider.TryGetNextRow(rowBuffer, cancellationToken))
                {
                    throw new InvalidOperationException($"JPX decode failed at row {rowIndex} (Image={Image.Name}).");
                }

                rowProcessor.WriteRow(rowIndex, rowBuffer);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return rowProcessor.GetDecoded();
        }
        finally
        {
            rowProcessor?.Dispose();
        }
    }

    /// <summary>
    /// Computes the optimal JPX descale factor by comparing the full image size
    /// to the target rendering size. The factor is the largest power of 2 that still
    /// produces an image at least as large as the target.
    /// </summary>
    private static JpxDecodingParameters ComputeDecodingParameters(
        JpxHeader header,
        ImageDecodingContext context)
    {
        var sourceSize = new SKSizeI((int)header.Width, (int)header.Height);
        SKSizeI? targetSize = context.GetScaledSize(sourceSize);

        if (!targetSize.HasValue || header.CodingStyle == null)
        {
            return JpxDecodingParameters.Default;
        }

        int maxLevels = header.CodingStyle.DecompositionLevels;

        // Find the largest power-of-2 factor where the reduced image is still >= target
        int descaleFactor = 1;

        for (int candidate = 2; candidate <= (1 << maxLevels); candidate *= 2)
        {
            int reducedWidth = Math.Max(1, (sourceSize.Width + candidate - 1) / candidate);
            int reducedHeight = Math.Max(1, (sourceSize.Height + candidate - 1) / candidate);

            if (reducedWidth >= targetSize.Value.Width && reducedHeight >= targetSize.Value.Height)
            {
                descaleFactor = candidate;
            }
            else
            {
                break;
            }
        }

        return new JpxDecodingParameters(descaleFactor);
    }
}
