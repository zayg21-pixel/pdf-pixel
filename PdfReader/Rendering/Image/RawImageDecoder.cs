using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Image.Raw;
using PdfReader.Rendering.Image.Processing;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Decodes a raw PDF image (an image whose stream has already had its /Filter chain decoded).
    /// A raw image is any /Image XObject not handled by a specialized codec (JPEG, CCITT, JBIG2, JPEG2000, etc.).
    /// Responsibilities:
    ///  1. Undo predictor row-by-row when present (TIFF / PNG predictors).
    ///  2. Provide predictor‑undone, original bit depth sample rows to <see cref="PdfImageRowProcessor"/>.
    ///  3. Let the row processor handle /Decode arrays, color key masking, palette expansion, color conversion.
    /// Falls back to legacy full-buffer path for unsupported predictor + bit depth combinations.
    /// </summary>
    public class RawImageDecoder : PdfImageDecoder
    {
        public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
        {
        }

        /// <summary>
        /// Decode the raw image stream into an <see cref="SKImage"/> or return null if decoding fails.
        /// </summary>
        public override unsafe SKImage Decode()
        {
            if (!ValidateImageParameters())
            {
                return null;
            }

            ReadOnlyMemory<byte> sourceData = Image.GetImageData();
            if (sourceData.IsEmpty)
            {
                Logger.LogError("Raw image data is empty (Name={Name}).", Image.Name);
                return null;
            }

            // Try streaming row decoder first.
            try
            {
                return DecodeStreamed(sourceData);
            }
            catch (NotSupportedException nsx)
            {
                Logger.LogInformation(nsx, "Row streaming not supported; falling back to legacy full-buffer path (Name={Name}).", Image.Name);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Row streaming path failed; attempting legacy full-buffer path (Name={Name}).");
            }

            return null;
        }

        private unsafe SKImage DecodeStreamed(ReadOnlyMemory<byte> sourceData)
        {
            using PdfRawImageRowDecoder rowDecoder = new PdfRawImageRowDecoder(Image, sourceData);
            using PdfImageRowProcessor rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>());
            rowProcessor.InitializeBuffer();

            // Managed row buffer reused each iteration; size is original bit depth packed row.
            byte[] rowBuffer = new byte[rowDecoder.DecodedRowByteLength];

            int expectedRows = Image.Height;
            int rowIndex = 0;
            while (true)
            {
                bool hasRow;
                fixed (byte* rowPtr = rowBuffer)
                {
                    hasRow = rowDecoder.DecodeNexRow(rowPtr);
                    if (hasRow)
                    {
                        rowProcessor.WriteRow(rowIndex, rowPtr);
                    }
                }

                if (!hasRow)
                {
                    break;
                }

                rowIndex++;
                if (rowIndex > expectedRows)
                {
                    throw new InvalidOperationException($"Decoded more rows than expected ({rowIndex}>{expectedRows}) for image {Image.Name}.");
                }
            }

            if (rowIndex != expectedRows)
            {
                Logger.LogWarning("Row decoder ended early (Decoded={Decoded} Expected={Expected}) (Name={Name}).", rowIndex, expectedRows, Image.Name);
            }

            return rowProcessor.GetSkImage();
        }
    }
}
