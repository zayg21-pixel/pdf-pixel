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

    public override async Task<SKImage> DecodeAsync(
        ImageDecodingContext context,
        PdfRenderingParameters renderingParameters,
        CancellationToken cancellationToken)
    {
        if (!ValidateImageParameters())
        {
            return null;
        }

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

        Logger.LogDebug("JPX Header - Width: {Width}, Height: {Height}, Components: {Components}, Format: {Format}", 
            header.Width, header.Height, header.ComponentCount, header.IsRawCodestream ? "Raw Codestream" : "JP2");

        if (header.CodingStyle != null)
        {
            Logger.LogDebug("JPX Coding Style - Decomposition Levels: {Levels}, Transform: {Transform}, Progressive Order: {Order}",
                header.CodingStyle.DecompositionLevels, 
                header.CodingStyle.IsReversibleTransform ? "Reversible (5-3)" : "Irreversible (9-7)",
                header.CodingStyle.ProgressionOrder);
        }

        // Decode codestream into row provider
        ReadOnlySpan<byte> codestreamData = encodedImageData.Span.Slice(header.CodestreamOffset);
        var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
        var jpxDecoder = new JpxDecoder(tileDecoder);

        using var rowProvider = jpxDecoder.Decode(header, codestreamData);

        // Stream decoded data through PdfImageRowProcessor
        PdfImageRowProcessor rowProcessor = null;

        try
        {
            rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>(), context, renderingParameters);
            rowProcessor.InitializeBuffer();

            byte[] rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];

            for (int rowIndex = 0; rowIndex < rowProvider.Height; rowIndex++)
            {
                if (!rowProvider.TryGetNextRow(rowBuffer))
                {
                    throw new InvalidOperationException($"JPX decode failed at row {rowIndex} (Image={Image.Name}).");
                }

                rowProcessor.WriteRow(rowIndex, rowBuffer);

                if (renderingParameters.AsyncExecution)
                {
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            return rowProcessor.GetDecoded();
        }
        finally
        {
            rowProcessor?.Dispose();
        }
    }
}
