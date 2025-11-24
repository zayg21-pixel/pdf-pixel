using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Imaging.Skia;

namespace PdfReader.Imaging.Decoding;

/// <summary>
/// Decodes a raw PDF image (an image whose stream has already had its /Filter chain decoded including predictor undo).
/// Stream-based implementation to reduce memory pressure: reads one decoded (predictor-processed, packed) row
/// at a time from the underlying stream and forwards it to <see cref="PdfImageRowProcessor"/>.
/// Includes an experimental fast path that attempts to wrap compatible PDF Flate + PNG predictor data directly
/// into a PNG container without recompression.
/// </summary>
public class RawImageDecoder : PdfImageDecoder
{
    public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
    }

    /// <summary>
    /// Decode the raw image stream into an <see cref="SKImage"/> or return null if decoding fails.
    /// Attempts an experimental fast PNG wrapping path first (no recompression) when the encoded PDF image
    /// matches a restricted PNG compatible profile.
    /// </summary>
    public override PdfImageDecodingResult Decode()
    {
        if (!ValidateImageParameters())
        {
            return null;
        }

        PdfImageDecodingResult fastPng = PngSkiaDecoder.DecodeAsPng(Image);
        if (fastPng != null)
        {
            return fastPng;
        }

        using Stream dataStream = Image.GetImageDataStream();
        if (dataStream == null)
        {
            Logger.LogError("Raw image data stream is null (Name={Name}).", Image.Name);
            return null;
        }

        try
        {
            return DecodeStream(dataStream);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Raw image decode failed (Name={Name}).", Image.Name);
            return null;
        }
    }

    /// <summary>
    /// Stream-based row decoding: computes expected per-row byte count and processes each row sequentially.
    /// For bitsPerComponent &lt; 8 data remains packed; packing is handled downstream by the row processor.
    /// </summary>
    private PdfImageDecodingResult DecodeStream(Stream imageStream)
    {
        using PdfImageRowProcessor rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>());
        rowProcessor.InitializeBuffer();

        int imageHeight = Image.Height;
        int imageWidth = Image.Width;
        int componentCount = Image.ColorSpaceConverter.Components;
        int bitsPerComponent = Image.BitsPerComponent;

        // Compute packed bytes per input row (raw decoded samples prior to conversion).
        int decodedRowBytes;
        if (bitsPerComponent >= 8)
        {
            int bytesPerComponent = (bitsPerComponent + 7) / 8; // 8->1, 16->2
            decodedRowBytes = checked(imageWidth * componentCount * bytesPerComponent);
        }
        else
        {
            decodedRowBytes = checked((imageWidth * componentCount * bitsPerComponent + 7) / 8);
        }

        if (decodedRowBytes <= 0)
        {
            throw new InvalidOperationException("Computed decoded row byte count is invalid.");
        }

        // Allocate a dedicated row buffer (no shared pool) per user instructions.
        byte[] rowBuffer = new byte[decodedRowBytes];

        for (int rowIndex = 0; rowIndex < imageHeight; rowIndex++)
        {
            int bytesReadThisRow = 0;
            while (bytesReadThisRow < decodedRowBytes)
            {
                int read = imageStream.Read(rowBuffer, bytesReadThisRow, decodedRowBytes - bytesReadThisRow);
                if (read == 0)
                {
                    Logger.LogWarning("Premature end of raw image stream at row {Row}/{Height} (Name={Name}).", rowIndex, imageHeight, Image.Name);
                    return rowProcessor.GetDecoded();
                }
                bytesReadThisRow += read;
            }

            rowProcessor.WriteRow(rowIndex, rowBuffer);
        }

        return rowProcessor.GetDecoded();
    }
}
