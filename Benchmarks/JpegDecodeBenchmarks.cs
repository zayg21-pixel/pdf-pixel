using BenchmarkDotNet.Attributes;
using PdfReader.Rendering.Image.Jpg.Decoding;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Readers;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    [MemoryDiagnoser]
    //[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, invocationCount: 20, iterationCount: 32)]
    public class JpegDecodeBenchmarks
    {
        private ReadOnlyMemory<byte> jpegData;
        private JpgHeader header;
        private int width;
        private int height;
        private int components;
        private int rowStride;

        [GlobalSetup]
        public void Setup()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestJpeg.jpg"));
            jpegData = new ReadOnlyMemory<byte>(bytes);
            header = JpgReader.ParseHeader(jpegData.Span);
            width = header.Width;
            height = header.Height;
            components = header.ComponentCount; // Expected 3 (RGB)
            rowStride = checked(width * components);
        }

        [Benchmark]
        public SKBitmap SkiaDecode()
        {
            // Reference: Skia's own JPEG decode path (alloc + convert internal).
            return SKBitmap.Decode(jpegData.Span);
        }

        [Benchmark]
        public unsafe SKImage BaselineStreamDecode()
        {
            if (header.IsProgressive)
            {
                throw new InvalidOperationException("Test image must be baseline for this benchmark.");
            }

            if (components != 3)
            {
                throw new InvalidOperationException("Benchmark assumes an RGB (3 component) JPEG.");
            }

            ReadOnlyMemory<byte> compressed = jpegData.Slice(header.ContentOffset);
            JpgBaselineDecoder stream = new JpgBaselineDecoder(header, compressed);

            int rgbaStride = width * 4;
            int rgbaBufferSize = rgbaStride * height;

            IntPtr tempRowPtr = IntPtr.Zero;
            IntPtr rgbaPtr = IntPtr.Zero;
            try
            {
                tempRowPtr = Marshal.AllocHGlobal(rowStride);
                rgbaPtr = Marshal.AllocHGlobal(rgbaBufferSize);

                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    Span<byte> rgbRow = new Span<byte>((void*)tempRowPtr, rowStride);

                    if (!stream.TryReadRow(rgbRow))
                    {
                        throw new Exception();
                    }

                    byte* destRow = (byte*)rgbaPtr + rowIndex * rgbaStride;
                    for (int x = 0; x < width; x++)
                    {
                        int src = x * 3;
                        int dst = x * 4;
                        destRow[dst] = rgbRow[src];          // R
                        destRow[dst + 1] = rgbRow[src + 1];  // G
                        destRow[dst + 2] = rgbRow[src + 2];  // B
                        destRow[dst + 3] = 255;              // A (opaque)
                    }
                }

                // Mirror PdfImageRowProcessor approach: wrap pointer with SKPixmap and create SKImage that owns and frees it.
                SKColorType colorType = SKColorType.Rgba8888;
                SKAlphaType alphaType = SKAlphaType.Unpremul;
                SKImageInfo info = new SKImageInfo(width, height, colorType, alphaType);
                using SKPixmap pixmap = new SKPixmap(info, rgbaPtr, rgbaStride);
                SKImageRasterReleaseDelegate release = (addr, ctx) =>
                {
                    if (addr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(addr);
                    }
                };
                SKImage image = SKImage.FromPixels(pixmap, release);
                if (image == null)
                {
                    throw new InvalidOperationException("Failed to create SKImage from unmanaged buffer.");
                }
                rgbaPtr = IntPtr.Zero; // Ownership transferred to Skia via release delegate.
                return image;
            }
            finally
            {
                if (tempRowPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(tempRowPtr);
                }
                if (rgbaPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(rgbaPtr); // Only freed here if SKImage creation failed.
                }
            }
        }
    }
}
