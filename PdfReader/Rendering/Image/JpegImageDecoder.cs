using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using PdfReader.Rendering.Image.Jpg.Decoding;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Processing;
using PdfReader.Streams;
using PdfReader.Rendering.Image.Jpg.Color;
using PdfReader.Rendering.Color;
using Microsoft.Extensions.Logging;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// JPEG (DCTDecode) image decoder.
    /// Decodes a JPEG-encoded image stream (baseline or progressive) into an interleaved component buffer and
    /// delegates final interpretation (/Decode mapping, color conversion, masking, palette handling, etc.) to
    /// <see cref="PdfImageProcessor"/>. The decoder performs any mandatory color transforms declared by the
    /// image data (e.g. YCbCr ➜ RGB, YCCK ➜ CMYK) while streaming MCUs so that the output buffer is already
    /// in the target component space (Gray = 1, RGB = 3, CMYK = 4 components).
    ///
    /// TODO / Work Plan:
    ///  - Chroma upsampling quality improvements (filtered upsampling for 4:2:0 / 4:2:2 in fidelity mode).
    ///  - 12‑bit sample precision support (widen IDCT + scaling pipeline).
    ///  - Arithmetic coding (SOF9 / SOF10) support.
    ///  - Lossless / hierarchical modes.
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
                try
                {
                    return SKImage.FromEncodedData(encodedImageData.Span);
                }
                catch (Exception fallbackEx)
                {
                    Logger.LogError(fallbackEx, "Skia fallback JPEG decode failed (Name={Name}).", Image.Name);
                    return null;
                }
            }
        }

        /// <summary>
        /// Decode using custom streaming pipeline. Never returns null; throws on failure.
        /// </summary>
        private unsafe SKImage DecodeInternal(ReadOnlyMemory<byte> encoded)
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

            int componentCount = header.ComponentCount;
            int rowStride = componentCount * imageWidth;
            int totalBytes = checked(rowStride * imageHeight);

            ContentStream jpegStream;
            try
            {
                ReadOnlyMemory<byte> compressed = encoded.Slice(header.ContentOffset);
                jpegStream = header.IsProgressive
                    ? (ContentStream)new JpgProgressiveStream(header, compressed)
                    : (ContentStream)new JpgBaselineStream(header, compressed);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"JPEG stream initialization failed (Image={Image.Name}).", ex);
            }

            IntPtr unmanagedBuffer = IntPtr.Zero;
            try
            {
                unmanagedBuffer = Marshal.AllocHGlobal(totalBytes);
                Span<byte> allBytes = new Span<byte>((void*)unmanagedBuffer, totalBytes);

                for (int rowIndex = 0; rowIndex < imageHeight; rowIndex++)
                {
                    Span<byte> targetRow = allBytes.Slice(rowIndex * rowStride, rowStride);
                    int remaining = rowStride;
                    int writeOffset = 0;
                    while (remaining > 0)
                    {
                        int bytesRead = jpegStream.Read(targetRow.Slice(writeOffset, remaining));
                        if (bytesRead <= 0)
                        {
                            throw new InvalidOperationException($"Unexpected end of JPEG stream at row {rowIndex} (Image={Image.Name}).");
                        }
                        writeOffset += bytesRead;
                        remaining -= bytesRead;
                    }
                }

                return Processor.CreateImage(allBytes);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuffer);
                jpegStream.Dispose();
            }
        }
    }
}
