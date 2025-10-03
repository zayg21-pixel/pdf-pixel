using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.PostProcessing
{
    /// <summary>
    /// Post-processes a fully decoded (filter chain + predictor already undone) PDF image sample buffer.
    /// RAW sample domains used:
    ///  * 1 bpc  -> codes 0..1
    ///  * 2 bpc  -> codes 0..3
    ///  * 4 bpc  -> codes 0..15
    ///  * 8 bpc  -> codes 0..255
    ///  * 16 bpc -> high byte only (0..255)
    /// This class now forwards raw codes directly to the variable-sized decode LUTs built by PostProcessingUtilities.
    /// </summary>
    public static unsafe class PdfImagePostProcessor
    {
        /// <summary>
        /// Creates an SKImage directly from the decoded buffer without any post-processing.
        /// This method performs minimal operations - just wraps the buffer in an SKImage.
        /// No color space conversion, decode arrays, or masking is applied.
        /// The provided imageBuffer will be freed when the SKImage is disposed via Marshal.FreeHGlobal.
        /// </summary>
        public static unsafe SKImage CreateImage(PdfImage image, IntPtr imageBuffer)
        {
            if (image == null)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.CreateImage: image is null.");
                return null;
            }

            if (imageBuffer == IntPtr.Zero)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.CreateImage: image buffer is null.");
                return null;
            }

            int width = image.Width;
            int height = image.Height;
            if (width <= 0 || height <= 0)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.CreateImage: invalid dimensions.");
                return null;
            }

            // Determine color type and bytes per pixel from image metadata
            SKColorType colorType;
            SKAlphaType alphaType;
            int bytesPerPixel;

            if (image.HasImageMask || image.IsSoftMask)
            {
                // Alpha/mask images use single channel alpha8
                colorType = SKColorType.Alpha8;
                alphaType = SKAlphaType.Unpremul;
                bytesPerPixel = 1;
            }
            else
            {
                var converter = image.ColorSpaceConverter;
                if (converter == null)
                {
                    // Default to grayscale if no converter (defensive fallback)
                    colorType = SKColorType.Gray8;
                    alphaType = SKAlphaType.Opaque;
                    bytesPerPixel = 1;
                }
                else if (converter.Components == 1)
                {
                    colorType = SKColorType.Gray8;
                    alphaType = SKAlphaType.Opaque;
                    bytesPerPixel = 1;
                }
                else if (converter.Components == 3)
                {
                    colorType = SKColorType.Rgb888x;
                    alphaType = SKAlphaType.Opaque;
                    bytesPerPixel = 3;
                }
                else if (converter.Components == 4)
                {
                    Console.Error.WriteLine("PdfImagePostProcessor.CreateImage: CMYK not supported directly.");
                    return null;
                }
                else
                {
                    Console.Error.WriteLine("PdfImagePostProcessor.CreateImage: unsupported component count.");
                    return null;
                }
            }

            int rowBytes = width * bytesPerPixel;
            var info = new SKImageInfo(width, height, colorType, alphaType);

            try
            {
                using var pixmap = new SKPixmap(info, imageBuffer, rowBytes);
                SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
                return SKImage.FromPixels(pixmap, release);
            }
            catch (System.Exception ex)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.CreateImage failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Determines whether full post-processing (decode arrays, masking, color conversion) is required.
        /// Fast paths allow direct wrapping when: 8 bpc, device gray/rgb, no decode array, no masking.
        /// </summary>
        public static bool RequiresPostProcess(PdfImage image)
        {
            if (image == null)
            {
                return true;
            }

            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                return false;
            }

            // Non-8-bit always requires conversion and scaling logic here
            if (image.BitsPerComponent != 8)
            {
                return true;
            }

            if (image.DecodeArray != null && image.DecodeArray.Count > 0)
            {
                return true;
            }

            if (image.MaskArray != null && image.MaskArray.Count >= converter.Components * 2)
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

            if (image.IsSoftMask && image.DecodeArray != null && image.DecodeArray.Count >= 2)
            {
                if (System.Math.Abs(image.DecodeArray[0] - 0f) > 1e-6f || System.Math.Abs(image.DecodeArray[1] - 1f) > 1e-6f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Full post-processing pipeline entry: handles both direct and indexed color images and mask images.
        /// </summary>
        public static unsafe SKImage PostProcess(PdfImage image, IntPtr decodedBuffer)
        {
            if (image == null)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: image is null.");
                return null;
            }

            if (decodedBuffer == IntPtr.Zero)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: decoded buffer is null.");
                return null;
            }

            byte* decoded = (byte*)decodedBuffer.ToPointer();
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            if (width <= 0 || height <= 0)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: invalid dimensions.");
                return null;
            }

            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: missing color space converter.");
                return null;
            }

            int components = converter.Components;
            if (components <= 0)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: invalid component count.");
                return null;
            }

            if (image.HasImageMask || image.IsSoftMask)
            {
                return ProcessAlphaMask(image, decoded);
            }

            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: unsupported bits per component.");
                return null;
            }

            bool isIndexed = converter is IndexedConverter;
            if (isIndexed)
            {
                if (components != 1)
                {
                    Console.Error.WriteLine("PdfImagePostProcessor.PostProcess: Indexed image must have 1 component.");
                    return null;
                }
                return ProcessIndexed(image, decoded, (IndexedConverter)converter);
            }

            return ProcessDirect(image, decoded);
        }

        private static int GetRowByteLength(int width, int components, int bitsPerComponent)
        {
            if (bitsPerComponent >= 8)
            {
                int bytesPerComponent = bitsPerComponent / 8;
                return width * components * bytesPerComponent;
            }
            long bitsPerRow = (long)width * components * bitsPerComponent;
            long padded = (bitsPerRow + 7) & ~7L;
            return (int)(padded / 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ReadSampleRaw(byte* decoded, int rowIndex, int columnIndex, int componentIndex, int width, int components, int bitsPerComponent, int rowByteLength)
        {
            if (bitsPerComponent == 8)
            {
                int index = rowIndex * rowByteLength + (columnIndex * components + componentIndex);
                return decoded[index];
            }
            if (bitsPerComponent == 16)
            {
                int index = rowIndex * rowByteLength + (columnIndex * components + componentIndex) * 2;
                return decoded[index];
            }
            int sampleIndexInRow = columnIndex * components + componentIndex;
            int bitOffsetInRow = sampleIndexInRow * bitsPerComponent;
            int absoluteBitOffset = rowIndex * rowByteLength * 8 + bitOffsetInRow;
            int byteIndex = absoluteBitOffset >> 3;
            int bitIndexWithinByte = absoluteBitOffset & 7;
            switch (bitsPerComponent)
            {
                case 1:
                {
                    int bitPos = 7 - bitIndexWithinByte;
                    return (decoded[byteIndex] >> bitPos) & 0x1;
                }
                case 2:
                {
                    int aligned = bitIndexWithinByte & 6;
                    int shift = 6 - aligned;
                    return (decoded[byteIndex] >> shift) & 0x3;
                }
                case 4:
                {
                    bool high = (bitIndexWithinByte & 4) == 0;
                    return high ? ((decoded[byteIndex] >> 4) & 0xF) : (decoded[byteIndex] & 0xF);
                }
                default:
                {
                    return 0;
                }
            }
        }

        private static unsafe SKImage ProcessDirect(PdfImage image, byte* decoded)
        {
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                Console.Error.WriteLine("PdfImagePostProcessor.ProcessDirectRgba: missing color space converter.");
                return null;
            }

            int components = converter.Components;
            bool hasColorKeyMask = image.MaskArray != null && image.MaskArray.Count >= components * 2;
            int[] minInclusive = null;
            int[] maxInclusive = null;
            if (hasColorKeyMask)
            {
                if (!PostProcessingUtilities.TryBuildColorKeyRanges(components, bitsPerComponent, image.MaskArray, out minInclusive, out maxInclusive))
                {
                    Console.Error.WriteLine("PdfImagePostProcessor.ProcessDirectRgba: invalid /Mask array; ignoring color key mask.");
                    hasColorKeyMask = false;
                }
            }

            // Build decode LUTs once (each component => 256 entries) based on /Decode and bit depth rules
            float[][] decodeLuts;
            {
                float[] flat = null;
                if (image.DecodeArray != null && image.DecodeArray.Count > 0)
                {
                    flat = new float[image.DecodeArray.Count];
                    for (int i = 0; i < flat.Length; i++)
                    {
                        flat[i] = image.DecodeArray[i];
                    }
                }
                ReadOnlySpan<float> decodeSpan = flat == null ? default : new ReadOnlySpan<float>(flat);
                decodeLuts = PostProcessingUtilities.BuildDecodeLuts(components, bitsPerComponent, decodeSpan);
            }

            int rowByteLength = GetRowByteLength(width, components, bitsPerComponent);
            int destinationRowBytes = width * 4;
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
                            int rawCode = ReadSampleRaw(decoded, rowIndex, columnIndex, componentIndex, width, components, bitsPerComponent, rowByteLength);

                            if (rawCode >= decodeLuts[componentIndex].Length)
                            {
                                rawCode = decodeLuts[componentIndex].Length - 1; // defensive clamp
                            }
                            if (masked)
                            {
                                if (rawCode < minInclusive[componentIndex] || rawCode > maxInclusive[componentIndex])
                                {
                                    masked = false;
                                }
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
            catch (System.Exception ex)
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
                Console.Error.WriteLine("PdfImagePostProcessor.ProcessDirectRgba failed: " + ex.Message);
                return null;
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
                Console.Error.WriteLine("PdfImagePostProcessor.ProcessIndexed: empty palette.");
                return null;
            }

            float decodeMin = 0f;
            float decodeMax = palette.Length - 1;
            var decodeArray = image.DecodeArray;
            if (decodeArray != null && decodeArray.Count >= 2)
            {
                decodeMin = decodeArray[0];
                decodeMax = decodeArray[1];
            }

            int[] indexMap = PostProcessingUtilities.BuildIndexedDecodeMap(palette.Length, bitsPerComponent, decodeMin, decodeMax);
            int rowByteLength = GetRowByteLength(width, 1, bitsPerComponent);
            int destinationRowBytes = width * 4;
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
                        int rawCode = ReadSampleRaw(decoded, rowIndex, columnIndex, 0, width, 1, bitsPerComponent, rowByteLength);
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
            catch (System.Exception ex)
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
                Console.Error.WriteLine("PdfImagePostProcessor.ProcessIndexed failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Alpha / soft mask image processing. Produces an Alpha8 SKImage. For soft masks /Decode may remap
        /// the normalized coverage range. For image masks (1 bpc) inversion is controlled by decode ordering.
        /// </summary>
        private static unsafe SKImage ProcessAlphaMask(PdfImage image, byte* decoded)
        {
            int width = image.Width;
            int height = image.Height;
            int bitsPerComponent = image.BitsPerComponent;
            var decodeArray = image.DecodeArray;

            byte[] alphaLut;
            if (image.HasImageMask)
            {
                // Image mask: two-entry LUT based on decode ordering (default [0 1]) or inverted [1 0]
                bool invert = false;
                if (decodeArray != null && decodeArray.Count >= 2)
                {
                    invert = decodeArray[0] > decodeArray[1];
                }
                alphaLut = new byte[2];
                alphaLut[0] = invert ? (byte)0 : (byte)255;
                alphaLut[1] = invert ? (byte)255 : (byte)0;
            }
            else
            {
                // Soft mask: build full LUT according to decode range (supports arbitrary scaling/inversion)
                float decodeMin = 0f;
                float decodeMax = 1f;
                if (decodeArray != null && decodeArray.Count >= 2)
                {
                    decodeMin = decodeArray[0];
                    decodeMax = decodeArray[1];
                }
                int effectiveBpc = bitsPerComponent == 16 ? 16 : bitsPerComponent;
                alphaLut = PostProcessingUtilities.BuildAlphaLut(effectiveBpc, decodeMin, decodeMax);
            }

            IntPtr alphaPtr = IntPtr.Zero;
            try
            {
                alphaPtr = Marshal.AllocHGlobal(width * height);
                byte* dst = (byte*)alphaPtr.ToPointer();
                int rowByteLength = GetRowByteLength(width, 1, bitsPerComponent);

                Parallel.For(0, height, rowIndex =>
                {
                    for (int columnIndex = 0; columnIndex < width; columnIndex++)
                    {
                        int rawCode = ReadSampleRaw(decoded, rowIndex, columnIndex, 0, width, 1, bitsPerComponent, rowByteLength);
                        if (rawCode >= alphaLut.Length)
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
            catch (System.Exception ex)
            {
                if (alphaPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(alphaPtr);
                }
                Console.Error.WriteLine("PdfImagePostProcessor.ProcessAlphaMask failed: " + ex.Message);
                return null;
            }
        }
    }
}