using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Color;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Produces a final <see cref="SKImage"/> from a fully decoded (filter and predictor already undone) PDF image sample buffer.
    /// </summary>
    public unsafe class PdfImageProcessor
    {
        private readonly PdfImage _image;
        private readonly ILogger<PdfImageProcessor> _logger;

        public PdfImageProcessor(PdfImage image, ILoggerFactory loggerFactory)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _logger = loggerFactory.CreateLogger<PdfImageProcessor>();
        }

        /// <summary>
        /// Creates a final <see cref="SKImage"/> from a decoded PDF image sample buffer.
        /// </summary>
        /// <param name="imageBuffer">Pointer to unmanaged memory containing the decoded (post-filter, post-predictor) raw sample codes.</param>
        /// <returns>Skia image ready for drawing.</returns>
        public SKImage CreateImage(ReadOnlySpan<byte> imageBuffer)
        {
            if (imageBuffer.IsEmpty)
            {
                _logger.LogError("Image buffer pointer is empty.");
                throw new ArgumentNullException(nameof(imageBuffer));
            }

            fixed (byte* data = &MemoryMarshal.GetReference(imageBuffer))
            {
                if (!RequiresPostProcess(_image))
                {
                    return CreateUnprocessedImage(data);
                }

                return PostProcess(data);
            }
        }

        /// <summary>
        /// Fast path for 8 bpc DeviceGray / DeviceRGB images without /Decode array or masking.
        /// The provided buffer is owned by the resulting SKImage and freed upon disposal. If image creation fails the buffer is freed here.
        /// </summary>
        private SKImage CreateUnprocessedImage(byte* imageBuffer)
        {
            int width = _image.Width;
            int height = _image.Height;
            if (width <= 0 || height <= 0)
            {
                _logger.LogError("Invalid image dimensions {Width}x{Height}.", width, height);
                throw new ArgumentException("Image dimensions must be positive.");
            }

            SKColorType colorType;
            SKAlphaType alphaType;
            IntPtr dest;
            int destBytesPerPixel;

            if (_image.HasImageMask || _image.IsSoftMask)
            {
                // Treat as single channel alpha
                colorType = SKColorType.Alpha8;
                alphaType = SKAlphaType.Unpremul;
                destBytesPerPixel = 1;
                var destLength = checked(width * height * destBytesPerPixel);
                dest = Marshal.AllocHGlobal(destLength);
                Buffer.MemoryCopy(imageBuffer, (void*)dest, destLength, destLength);
            }
            else
            {
                var converter = _image.ColorSpaceConverter;
                if (converter == null || converter.Components == 1)
                {
                    colorType = SKColorType.Gray8;
                    alphaType = SKAlphaType.Opaque;
                    destBytesPerPixel = 1;
                    var destLength = checked(width * height);
                    dest = Marshal.AllocHGlobal(destLength);
                    Buffer.MemoryCopy(imageBuffer, (void*)dest, destLength, destLength);
                }
                else if (converter.Components == 3)
                {
                    // Expand RGB -> RGBA (opaque alpha)
                    colorType = SKColorType.Rgba8888;
                    alphaType = SKAlphaType.Unpremul;
                    destBytesPerPixel = 4;
                    var destLength = checked(width * height * destBytesPerPixel);
                    dest = Marshal.AllocHGlobal(destLength);

                    byte* src = imageBuffer;
                    byte* dst = (byte*)dest;
                    int pixelCount = width * height;
                    for (int i = 0; i < pixelCount; i++)
                    {
                        dst[0] = src[0];
                        dst[1] = src[1];
                        dst[2] = src[2];
                        dst[3] = 255;
                        src += 3;
                        dst += 4;
                    }
                }
                else
                {
                    _logger.LogError("Unsupported component count {Components} for fast path.", converter?.Components);
                    throw new NotSupportedException("Unsupported component count for fast path.");
                }
            }

            try
            {

                int rowBytes = checked(width * destBytesPerPixel);
                var info = new SKImageInfo(width, height, colorType, alphaType);
                using var pixmap = new SKPixmap(info, dest, rowBytes);
                SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
                var image = SKImage.FromPixels(pixmap, release);
                if (image == null)
                {
                    throw new InvalidOperationException("SKImage.FromPixels returned null in fast path.");
                }
                return image;
            }
            catch
            {
                if (dest != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(dest);
                }
                throw;
            }
        }

        /// <summary>
        /// Determines whether full post-processing (decode arrays, masking, color conversion) is required.
        /// </summary>
        private static bool RequiresPostProcess(PdfImage image)
        {
            if (image == null)
            {
                return true;
            }

            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                return false; // treat as grayscale fast path
            }

            if (image.BitsPerComponent != 8)
            {
                return true;
            }
            if (image.DecodeArray != null && image.DecodeArray.Length > 0)
            {
                return true;
            }
            if (image.MaskArray != null && image.MaskArray.Length >= converter.Components * 2)
            {
                return true;
            }
            if (!(converter is DeviceRgbConverter) && !(converter is DeviceGrayConverter))
            {
                return true;
            }
            if (image.HasImageMask)
            {
                return true;
            }
            if (image.IsSoftMask && ProcessingUtilities.ApplyDecode(image.DecodeArray))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Post-process pipeline for images not handled by the fast path. Does not free the input buffer (caller manages lifetime).
        /// </summary>
        private unsafe SKImage PostProcess(byte* decoded)
        {
            int width = _image.Width;
            int height = _image.Height;
            int bitsPerComponent = _image.BitsPerComponent;
            if (width <= 0 || height <= 0)
            {
                _logger.LogError("Invalid image dimensions {Width}x{Height}.", width, height);
                throw new ArgumentException("Image dimensions must be positive.");
            }

            var converter = _image.ColorSpaceConverter;
            if (converter == null)
            {
                _logger.LogError("Missing color space converter.");
                throw new InvalidOperationException("Missing color space converter.");
            }

            int components = converter.Components;
            if (components <= 0)
            {
                _logger.LogError("Invalid component count {Components}.", components);
                throw new InvalidOperationException("Invalid component count.");
            }

            if (_image.HasImageMask || _image.IsSoftMask)
            {
                return ProcessAlphaMask(_image, decoded);
            }

            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                _logger.LogError("Unsupported bits-per-component value {BitsPerComponent}.", bitsPerComponent);
                throw new NotSupportedException("Unsupported bits per component.");
            }

            if (converter is IndexedConverter indexed)
            {
                if (components != 1)
                {
                    _logger.LogError("Indexed color space must have exactly 1 component; found {Components}.", components);
                    throw new InvalidOperationException("Indexed image must have exactly 1 component.");
                }
                return ProcessIndexed(_image, decoded, indexed);
            }

            return ProcessDirect(_image, decoded);
        }

        private static unsafe SKImage ProcessDirect(PdfImage image, byte* decoded)
        {
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                throw new InvalidOperationException("ProcessDirect: Missing color space converter.");
            }

            int components = converter.Components;
            bool hasColorKeyMask = ProcessingUtilities.TryBuildColorKeyRanges(components, bitsPerComponent, image.MaskArray, out var minInclusive, out var maxInclusive);
            float[][] decodeLuts = ProcessingUtilities.BuildDecodeLuts(components, bitsPerComponent, image.DecodeArray);

            int rowByteLength = ImageSampling.GetRowByteLength(width, components, bitsPerComponent);
            int destinationRowBytes = checked(width * 4);
            IntPtr bufferPtr = IntPtr.Zero;

            try
            {
                bufferPtr = Marshal.AllocHGlobal(destinationRowBytes * height);
                byte* dstBase = (byte*)bufferPtr.ToPointer();

                Parallel.For(0, height, rowIndex =>
                {
                    byte* dstRow = dstBase + rowIndex * destinationRowBytes;
                    int dstIndex = 0;

                    for (int columnIndex = 0; columnIndex < width; columnIndex++)
                    {
                        bool masked = hasColorKeyMask;
                        Span<float> comps01 = stackalloc float[components];

                        for (int componentIndex = 0; componentIndex < components; componentIndex++)
                        {
                            int rawCode = ImageSampling.ReadSampleRaw(decoded, rowIndex, columnIndex, componentIndex, width, components, bitsPerComponent, rowByteLength);

                            if ((uint)rawCode >= (uint)decodeLuts[componentIndex].Length)
                            {
                                rawCode = decodeLuts[componentIndex].Length - 1; // clamp
                            }
                            if (masked && (rawCode < minInclusive[componentIndex] || rawCode > maxInclusive[componentIndex]))
                            {
                                masked = false;
                            }
                            comps01[componentIndex] = decodeLuts[componentIndex][rawCode];
                        }

                        var srgb = converter.ToSrgb(comps01, image.RenderingIntent);
                        dstRow[dstIndex++] = srgb.Red;
                        dstRow[dstIndex++] = srgb.Green;
                        dstRow[dstIndex++] = srgb.Blue;
                        dstRow[dstIndex++] = masked ? (byte)0 : (byte)255;
                    }
                });

                var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                using var pixmap = new SKPixmap(info, bufferPtr, destinationRowBytes);
                SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
                return SKImage.FromPixels(pixmap, release);
            }
            catch
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
                throw;
            }
        }

        private static unsafe SKImage ProcessIndexed(PdfImage image, byte* decoded, IndexedConverter indexedConverter)
        {
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            var palette = indexedConverter.BuildPalette(image.RenderingIntent);
            if (palette == null || palette.Length == 0)
            {
                throw new InvalidOperationException("ProcessIndexed: Empty palette.");
            }

            int[] indexMap = ProcessingUtilities.BuildIndexedDecodeMap(palette.Length, bitsPerComponent, image.DecodeArray);
            int rowByteLength = ImageSampling.GetRowByteLength(width, 1, bitsPerComponent);
            int destinationRowBytes = checked(width * 4);
            IntPtr bufferPtr = IntPtr.Zero;

            try
            {
                bufferPtr = Marshal.AllocHGlobal(destinationRowBytes * height);
                byte* dstBase = (byte*)bufferPtr.ToPointer();

                Parallel.For(0, height, rowIndex =>
                {
                    byte* dstRow = dstBase + rowIndex * destinationRowBytes;
                    int dstIndex = 0;
                    for (int columnIndex = 0; columnIndex < width; columnIndex++)
                    {
                        int rawCode = ImageSampling.ReadSampleRaw(decoded, rowIndex, columnIndex, 0, width, 1, bitsPerComponent, rowByteLength);
                        if ((uint)rawCode >= (uint)indexMap.Length)
                        {
                            rawCode = 0;
                        }
                        int paletteIndex = indexMap[rawCode];
                        SKColor color = (paletteIndex >= 0 && paletteIndex < palette.Length) ? palette[paletteIndex] : SKColors.Black;
                        dstRow[dstIndex++] = color.Red;
                        dstRow[dstIndex++] = color.Green;
                        dstRow[dstIndex++] = color.Blue;
                        dstRow[dstIndex++] = 255;
                    }
                });

                var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                using var pixmap = new SKPixmap(info, bufferPtr, destinationRowBytes);
                SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
                return SKImage.FromPixels(pixmap, release);
            }
            catch
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
                throw;
            }
        }

        private static unsafe SKImage ProcessAlphaMask(PdfImage image, byte* decoded)
        {
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            var decodeArray = image.DecodeArray;

            byte[] alphaLut = image.HasImageMask
                ? ProcessingUtilities.BuildImageMaskLut(decodeArray)
                : ProcessingUtilities.BuildAlphaLut(bitsPerComponent, decodeArray);

            IntPtr alphaPtr = IntPtr.Zero;
            try
            {
                alphaPtr = Marshal.AllocHGlobal(width * height);
                byte* dst = (byte*)alphaPtr.ToPointer();
                int rowByteLength = ImageSampling.GetRowByteLength(width, 1, bitsPerComponent);

                Parallel.For(0, height, rowIndex =>
                {
                    for (int columnIndex = 0; columnIndex < width; columnIndex++)
                    {
                        int rawCode = ImageSampling.ReadSampleRaw(decoded, rowIndex, columnIndex, 0, width, 1, bitsPerComponent, rowByteLength);
                        if ((uint)rawCode >= (uint)alphaLut.Length)
                        {
                            rawCode = alphaLut.Length - 1;
                        }
                        dst[rowIndex * width + columnIndex] = alphaLut[rawCode];
                    }
                });

                var info = new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Unpremul);
                using var pixmap = new SKPixmap(info, alphaPtr, width);
                SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
                return SKImage.FromPixels(pixmap, release);
            }
            catch
            {
                if (alphaPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(alphaPtr);
                }
                throw;
            }
        }
    }
}