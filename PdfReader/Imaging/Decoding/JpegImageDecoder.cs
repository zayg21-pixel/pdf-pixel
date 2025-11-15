using Microsoft.Extensions.Logging;
using PdfReader.Color.ColorSpace;
using PdfReader.Imaging.Jpg.Color;
using PdfReader.Imaging.Jpg.Decoding;
using PdfReader.Imaging.Jpg.Model;
using PdfReader.Imaging.Jpg.Readers;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Imaging.Skia;
using SkiaSharp;
using System;

namespace PdfReader.Imaging.Decoding
{
    /// <summary>
    /// JPEG (DCTDecode) image decoder.
    /// Decodes a JPEG-encoded image stream (baseline or progressive) into an interleaved component row stream and
    /// performs row-level post processing (decode mapping, masking, color conversion, palette handling) via
    /// <see cref="PdfImageRowProcessor"/>. Mandatory JPEG color transforms (YCbCr ➜ RGB, YCCK ➜ CMYK) are applied
    /// during MCU decode so that emitted rows are already in the declared component space (Gray=1, RGB=3, CMYK=4).
    /// </summary>
    public sealed class JpegImageDecoder : PdfImageDecoder
    {
        public JpegImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
        {
        }

        /// <summary>
        /// Decode the JPEG image returning an <see cref="SKImage"/> or null on failure (errors are logged).
        /// Attempts custom streaming decode first; falls back to Skia's built‑in decoder if custom path fails.
        /// </summary>
        public override SKImage Decode()
        {
            if (!ValidateImageParameters())
            {
                return null;
            }

            var skiaJpg = JpgSkiaDecoder.DecodeAsJpg(Image);

            if (skiaJpg != null)
            {
                return skiaJpg;
            }

            ReadOnlyMemory<byte> encodedImageData = Image.GetImageData();

            if (encodedImageData.Length == 0)
            {
                Logger.LogError("JPEG image data is empty (Name={Name}).", Image.Name);
                return null;
            }

            try
            {
                return DecodeInternal(encodedImageData);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Primary JPEG decode path failed; attempting Skia fallback (Name={Name}).", Image.Name);
                return SKImage.FromEncodedData(encodedImageData.Span);
            }
        }

        /// <summary>
        /// Decode using custom streaming pipeline. Throws on failure.
        /// Row data is streamed row-by-row directly into a <see cref="PdfImageRowProcessor"/> without allocating a full intermediate buffer.
        /// </summary>
        private SKImage DecodeInternal(ReadOnlyMemory<byte> encoded)
        {
            JpgHeader header;
            try
            {
                header = JpgReader.ParseHeader(encoded.Span);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"JPEG header parse exception (Image={Image.Name}).", ex);
            }

            if (header == null || header.ContentOffset < 0)
            {
                throw new InvalidOperationException($"JPEG header invalid or missing content segment (Image={Image.Name}).");
            }

            try
            {
                Image.UpdateColorSpace(header.ComponentCount);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Color space update failed (Image={Image.Name}).", ex);
            }

            try
            {
                if (Image.ColorSpaceConverter.IsDevice && JpgIccProfileReader.TryAssembleIccProfile(header, out var profileBytes))
                {
                    Image.UpdateColorSpace(new IccBasedConverter(header.ComponentCount, Image.ColorSpaceConverter, profileBytes));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Ignoring embedded ICC profile due to assembly failure (Name={Name}).", Image.Name);
            }

            int imageWidth = header.Width;
            int imageHeight = header.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                throw new InvalidOperationException($"Invalid JPEG dimensions Width={imageWidth} Height={imageHeight} (Image={Image.Name}).");
            }

            int componentCount = header.ComponentCount; // After internal color transform stage
            int rowStride = checked(componentCount * imageWidth);

            IJpgDecoder decoder;
            try
            {
                ReadOnlyMemory<byte> compressed = encoded.Slice(header.ContentOffset);
                decoder = header.IsProgressive
                    ? new JpgProgressiveDecoder(header, compressed)
                    : new JpgBaselineDecoder(header, compressed);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"JPEG decoder initialization failed (Image={Image.Name}).", ex);
            }

            PdfImageRowProcessor rowProcessor = null;

            try
            {
                rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>());
                rowProcessor.InitializeBuffer();

                Span<byte> rowBuffer = new byte[rowStride];

                for (int rowIndex = 0; rowIndex < imageHeight; rowIndex++)
                {
                    if (!decoder.TryReadRow(rowBuffer))
                    {
                        throw new InvalidOperationException($"JPEG decode failed at row {rowIndex} (Image={Image.Name}).");
                    }

                    rowProcessor.WriteRow(rowIndex, rowBuffer);
                }

                return rowProcessor.GetSkImage();
            }
            finally
            {
                rowProcessor?.Dispose();
            }
        }
    }
}
