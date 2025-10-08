using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Image.Processing;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Decodes a raw PDF image (an image whose stream has already had its /Filter chain decoded including predictor undo).
    /// Responsibilities now limited to passing already predictor-decoded, packed sample bytes to the row processor.
    /// </summary>
    public class RawImageDecoder : PdfImageDecoder
    {
        public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
        {
        }

        /// <summary>
        /// Decode the raw image stream into an SKImage or return null if decoding fails.
        /// </summary>
        public override SKImage Decode()
        {
            if (!ValidateImageParameters())
            {
                return null;
            }

            ReadOnlyMemory<byte> data = Image.GetImageData();
            if (data.IsEmpty)
            {
                Logger.LogError("Raw image data is empty (Name={Name}).", Image.Name);
                return null;
            }

            try
            {
                return DecodeBuffer(data);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Raw image decode failed (Name={Name}).", Image.Name);
                return null;
            }
        }

        private SKImage DecodeBuffer(ReadOnlyMemory<byte> buffer)
        {
            using PdfImageRowProcessor rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>());
            rowProcessor.InitializeBuffer();

            int height = Image.Height;
            int width = Image.Width;
            int components = Image.ColorSpaceConverter.Components;
            int bitsPerComponent = Image.BitsPerComponent;

            // Compute decoded row length (post predictor) matching PredictorDecodeStream logic.
            int decodedRowBytes = bitsPerComponent >= 8
                ? width * components * ((bitsPerComponent + 7) / 8)
                : (width * components * bitsPerComponent + 7) / 8;

            if (decodedRowBytes <= 0)
            {
                throw new InvalidOperationException("Computed decoded row length is invalid.");
            }
            if (buffer.Length < decodedRowBytes * height)
            {
                Logger.LogWarning("Raw image buffer smaller than expected (Have={Have} Expected>={Need}) (Name={Name}).", buffer.Length, decodedRowBytes * height, Image.Name);
            }

            var span = buffer.Span;
            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                int offset = rowIndex * decodedRowBytes;
                if (offset + decodedRowBytes > span.Length)
                {
                    break;
                }
                unsafe
                {
                    fixed (byte* rowPtr = span.Slice(offset, decodedRowBytes))
                    {
                        rowProcessor.WriteRow(rowIndex, rowPtr);
                    }
                }
            }

            return rowProcessor.GetSkImage();
        }
    }
}
