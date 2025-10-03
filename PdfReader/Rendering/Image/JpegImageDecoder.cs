using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using PdfReader.Rendering.Image.Jpg.Decoding;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.PostProcessing;
using PdfReader.Streams;
using PdfReader.Rendering.Image.Jpg.Color;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// JPEG (DCTDecode) image decoder.
    /// Implements a strict 3-step pipeline (mirrors <see cref="RawImageDecoder"/> semantics):
    ///  1. Decode: Stream JPEG entropy (baseline or progressive) into an interleaved sample buffer sized Width * Height * ComponentCount.
    ///     MCU writers perform any immediate color transform required by the PDF pipeline (e.g., YCbCr -> RGB, YCCK -> CMYK) so the buffer is already in final component space.
    ///  2. (Optional) PostProcess: If <see cref="PdfImagePostProcessor.RequiresPostProcess"/> is true, invoke full post-processing (decode array mapping, masking, color key ranges, palette, ICC, etc.).
    ///  3. Fast wrap: If no post processing is needed, wrap the decoded buffer directly via <see cref="PdfImagePostProcessor.CreateImage"/>.
    ///
    /// The decoder no longer expands 1 or 3 component images to 4 components up front; stride = ComponentCount * Width.
    /// CMYK (or converted YCCK) remains 4 components, RGB (or converted YCbCr) is 3, grayscale is 1.
    /// Buffer ownership is passed to the created SKImage (fast path) or freed after post-processing (slow path).
    /// </summary>
    /// <remarks>
    /// WORK PLAN / TODO (restored & updated):
    /// Performance / Quality Improvements:
    ///  - Chroma upsampling quality (currently nearest-neighbor per MCU); evaluate filtered upsampling for 4:2:0 and 4:2:2 when fidelity mode enabled.
    ///
    /// Deferred / Low Priority:
    ///  - 12-bit sample precision (SOF0/2 with 12 bits). Will require widening internal buffers and adjusted IDCT scaling.
    ///  - Arithmetic coding (SOF9/SOF10) support.
    ///  - Lossless / hierarchical modes.
    /// </remarks>
    public sealed class JpegImageDecoder : PdfImageDecoder
    {
        public JpegImageDecoder(PdfImage image) : base(image)
        {
        }

        public override SKImage Decode()
        {
            if (!ValidateImageParameters())
            {
                return null;
            }

            ReadOnlyMemory<byte> encoded = Image.GetImageData();
            if (encoded.Length == 0)
            {
                return null;
            }

            SKImage internalResult = DecodeInternal(encoded);
            if (internalResult != null)
            {
                return internalResult;
            }

            try
            {
                return SKImage.FromEncodedData(encoded.Span);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JpegImageDecoder.Decode: Skia fallback failed: " + ex.Message);
                return null;
            }
        }

        private unsafe SKImage DecodeInternal(ReadOnlyMemory<byte> encoded)
        {
            JpgHeader header;
            try
            {
                header = JpgReader.ParseHeader(encoded.Span);
                if (header == null || header.ContentOffset < 0)
                {
                    return null;
                }
                // Adjust image color space converter if component count disagrees with declared PDF color space.
                Image.UpdateColorSpace(header.ComponentCount);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JpegImageDecoder.DecodeInternal: header parse failed: " + ex.Message);
                return null;
            }

            if (Image.ColorSpaceConverter.IsDevice && JpgIccProfileReader.TryAssembleIccProfile(header, out var profileBytes))
            {
                Image.UpdateColorSpace(new IccBasedConverter(header.ComponentCount, Image.ColorSpaceConverter, profileBytes));
            }

            int width = header.Width;
            int height = header.Height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int stride = header.ComponentCount * width;
            int totalBytes = stride * height;
            IntPtr buffer = IntPtr.Zero;

            ReadOnlyMemory<byte> compressed = encoded.Slice(header.ContentOffset);
            ContentStream stream;
            try
            {
                stream = header.IsProgressive
                    ? (ContentStream)new JpgProgressiveStream(header, compressed, Image.ColorSpaceConverter)
                    : (ContentStream)new JpgBaselineStream(header, compressed, Image.ColorSpaceConverter);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JpegImageDecoder.DecodeInternal: stream init failed: " + ex.Message);
                return null;
            }

            try
            {
                buffer = Marshal.AllocHGlobal(totalBytes);
                Span<byte> all = new Span<byte>((void*)buffer, totalBytes);
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    Span<byte> row = all.Slice(rowIndex * stride, stride);
                    int remaining = stride;
                    int offset = 0;
                    while (remaining > 0)
                    {
                        int read = stream.Read(row.Slice(offset, remaining));
                        if (read <= 0)
                        {
                            throw new InvalidOperationException("Unexpected end of JPEG stream.");
                        }
                        offset += read;
                        remaining -= read;
                    }
                }

                bool needsPost = PdfImagePostProcessor.RequiresPostProcess(Image);
                if (needsPost)
                {
                    try
                    {
                        return PdfImagePostProcessor.PostProcess(Image, buffer);
                    }
                    finally
                    {
                        if (buffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
                else
                {
                    try
                    {
                        return PdfImagePostProcessor.CreateImage(Image, buffer);
                    }
                    catch
                    {
                        if (buffer != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JpegImageDecoder.DecodeInternal: decode failed: " + ex.Message);
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
                return null;
            }
        }
    }
}
