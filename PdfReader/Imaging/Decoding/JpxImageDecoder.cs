using Microsoft.Extensions.Logging;
using PdfReader.Imaging.Jpx.Decoding;
using PdfReader.Imaging.Jpx.Model;
using PdfReader.Imaging.Jpx.Parsing;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Rendering.State;
using SkiaSharp;
using System;

namespace PdfReader.Imaging.Decoding;

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

    public override SKImage Decode(PdfGraphicsState state, SKCanvas canvas)
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

        try
        {
            return DecodeInternal(encodedImageData, state, canvas);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "JPX decode failed; attempting Skia fallback (Name={Name}).", Image.Name);
            return SKImage.FromEncodedData(encodedImageData.Span);
        }
    }

    /// <summary>
    /// Decode using custom JPX implementation with tile-to-row conversion.
    /// </summary>
    private SKImage DecodeInternal(ReadOnlyMemory<byte> encoded, PdfGraphicsState state, SKCanvas canvas)
    {
        // Parse JPX header
        JpxHeader header;
        try
        {
            header = JpxReader.ParseHeader(encoded.Span);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JPX header parse exception (Image={Image.Name}).", ex);
        }

        if (header == null)
        {
            throw new InvalidOperationException($"JPX header invalid or missing (Image={Image.Name}).");
        }

        // Validate basic header information
        if (header.Width == 0 || header.Height == 0)
        {
            throw new InvalidOperationException($"Invalid JPX dimensions Width={header.Width} Height={header.Height} (Image={Image.Name}).");
        }

        if (header.ComponentCount == 0)
        {
            throw new InvalidOperationException($"Invalid JPX component count {header.ComponentCount} (Image={Image.Name}).");
        }

        // Log header information for debugging
        Logger.LogDebug("JPX Header - Width: {Width}, Height: {Height}, Components: {Components}, Format: {Format}", 
            header.Width, header.Height, header.ComponentCount, header.IsRawCodestream ? "Raw Codestream" : "JP2");

        if (header.CodingStyle != null)
        {
            Logger.LogDebug("JPX Coding Style - Decomposition Levels: {Levels}, Transform: {Transform}, Progressive Order: {Order}",
                header.CodingStyle.DecompositionLevels, 
                header.CodingStyle.IsReversibleTransform ? "Reversible (5-3)" : "Irreversible (9-7)",
                header.CodingStyle.ProgressionOrder);
        }

        // Extract codestream data
        ReadOnlySpan<byte> codestreamData = encoded.Span.Slice(header.CodestreamOffset);

        try
        {
            // Create the appropriate decoder based on the header
            var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
            var jpxDecoder = new JpxDecoder(tileDecoder);
            
            // Decode using tile-to-row conversion
            using var rowProvider = jpxDecoder.Decode(header, codestreamData);
            
            // Stream decoded data through PdfImageRowProcessor
            return ProcessWithRowProvider(rowProvider, state, canvas);
        }
        catch (NotImplementedException)
        {
            // Decoder not implemented yet, fall back to Skia
            Logger.LogInformation("JPX tile decoder not implemented for this image type, falling back to Skia decoder (Name={Name}).", Image.Name);
            return SKImage.FromEncodedData(encoded.Span);
        }
    }

    /// <summary>
    /// Process decoded JPX data using the existing PDF image row processor.
    /// </summary>
    private SKImage ProcessWithRowProvider(IJpxRowProvider rowProvider, PdfGraphicsState state, SKCanvas canvas)
    {
        PdfImageRowProcessor rowProcessor = null;
        
        try
        {
            rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>(), state, canvas);
            rowProcessor.InitializeBuffer();

            Span<byte> rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];

            for (int rowIndex = 0; rowIndex < rowProvider.Height; rowIndex++)
            {
                if (!rowProvider.TryGetNextRow(rowBuffer))
                {
                    throw new InvalidOperationException($"JPX decode failed at row {rowIndex} (Image={Image.Name}).");
                }

                rowProcessor.WriteRow(rowIndex, rowBuffer);
            }

            return rowProcessor.GetDecoded();
        }
        finally
        {
            rowProcessor?.Dispose();
        }
    }
}
