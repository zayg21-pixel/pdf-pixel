using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    public class WriteChannelsPerformance
    {
        const int columns = 8192;
        private readonly byte[] source = new byte[columns * 3];
        private readonly byte[] dest = new byte[columns * 4];

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)Random.Shared.Next(0, 255);
            }
        }

        [Benchmark]
        public unsafe byte[] TestNormal()
        {
            fixed (byte* rgbPtr = &source[0])
            {
                fixed (byte* rgbaPtr = &dest[0])
                {
                    UpsampleScaleRgb8(rgbaPtr, rgbaPtr, columns);
                }
            }

            return dest;
        }

        [Benchmark]
        public unsafe byte[] TestStruct()
        {
            fixed (byte* rgbPtr = &source[0])
            {
                fixed (byte* rgbaPtr = &dest[0])
                {
                    UpsampleScaled2(rgbaPtr, rgbaPtr, columns);
                }
            }

            return dest;
        }

        [Benchmark]
        public unsafe byte[] TestFast()
        {
            fixed (byte* rgbPtr = &source[0])
            {
                fixed (byte* rgbaPtr = &dest[0])
                {
                    FastConvert(rgbaPtr, rgbaPtr, columns);
                }
            }

            return dest;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaled2(byte* source, byte* destination, int columns)
        {
            RGB* rgb = (RGB*)source;
            RGBA* rgba = (RGBA*)destination;
            for (int i = 0; i < columns; i++)
            {
                RGB* cp = (RGB*)rgba;
                *cp = *rgb;
                rgba->a = 255;
                rgb++;
                rgba++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void FastConvert(byte* source, byte* destination, int columns)
        {
            for (long i = 0, offsetRgb = 0; i < columns; i++, offsetRgb += 3)
            {
                ((uint*)destination)[i] = *(uint*)(source + offsetRgb) | 0xff000000;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb8(byte* source, byte* destination, int columns)
        {
            uint* destinationUint = (uint*)destination;
            int pairs = columns >> 1;
            for (int p = 0; p < pairs; p++)
            {
                ulong block = Unsafe.ReadUnaligned<ulong>(source); // reads 8, uses first 6
                uint color0 = (uint)(block & 0xFFFFFF);
                uint color1 = (uint)((block >> 24) & 0xFFFFFF);
                destinationUint[0] = color0 | 0xFF000000U;
                destinationUint[1] = color1 | 0xFF000000U;
                destinationUint += 2;
                source += 6;
            }
            if ((columns & 1) != 0)
            {
                uint tail = (uint)(source[0] | (source[1] << 8) | (source[2] << 16)) | 0xFF000000U;
                *destinationUint = tail;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBA
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGB
        {
            public byte r;
            public byte g;
            public byte b;
        }
    }
}
