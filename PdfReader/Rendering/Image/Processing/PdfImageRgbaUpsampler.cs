using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    internal static unsafe class PdfImageRgbaUpsampler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int ReadRawSample(byte* rowPtr, int sampleIndex, int bitsPerComponent)
        {
            switch (bitsPerComponent)
            {
                case 16:
                    {
                        int byteIndex = sampleIndex * 2;
                        int hi = rowPtr[byteIndex];
                        int lo = rowPtr[byteIndex + 1];
                        return (hi << 8) | lo;
                    }
                case 8:
                    {
                        return rowPtr[sampleIndex];
                    }
                case 4:
                    {
                        int byteIndex = sampleIndex >> 1;
                        bool highNibble = (sampleIndex & 1) == 0;
                        int value = rowPtr[byteIndex];
                        return highNibble ? (value >> 4) & 0x0F : value & 0x0F;
                    }
                case 2:
                    {
                        int byteIndex = sampleIndex >> 2;
                        int shift = 6 - ((sampleIndex & 3) * 2);
                        return (rowPtr[byteIndex] >> shift) & 0x03;
                    }
                case 1:
                    {
                        int byteIndex = sampleIndex >> 3;
                        int shift = 7 - (sampleIndex & 7);
                        return (rowPtr[byteIndex] >> shift) & 0x01;
                    }
                default:
                    {
                        return 0;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpsampleScaleRgbaRow(byte* source, byte* destination, int columns, int components, int bitsPerComponent)
        {
            switch (components)
            {
                case 1:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleGray1(source, destination, columns);
                            break;
                        case 2:
                            UpsampleScaleGray2(source, destination, columns);
                            break;
                        case 4:
                            UpsampleScaleGray4(source, destination, columns);
                            break;
                        case 8:
                            UpsampleScaleGray8(source, destination, columns);
                            break;
                        case 16:
                            UpsampleScaleGray16(source, destination, columns);
                            break;
                    }
                    break;
                case 3:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleRgb1(source, destination, columns);
                            break;
                        case 2:
                            UpsampleScaleRgb2(source, destination, columns);
                            break;
                        case 4:
                            UpsampleScaleRgb4(source, destination, columns);
                            break;
                        case 8:
                            UpsampleScaleRgb8(source, destination, columns);
                            break;
                        case 16:
                            UpsampleScaleRgb16(source, destination, columns);
                            break;
                    }
                    break;
                case 4:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleCmyk1(source, destination, columns);
                            break;
                        case 2:
                            UpsampleScaleCmyk2(source, destination, columns);
                            break;
                        case 4:
                            UpsampleScaleCmyk4(source, destination, columns);
                            break;
                        case 8:
                            UpsampleScaleCmyk8(source, destination, columns);
                            break;
                        case 16:
                            UpsampleScaleCmyk16(source, destination, columns);
                            break;
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray1(byte* source, byte* destination, int columns)
        {
            // 1-bit grayscale: each bit is one pixel (highest bit first). Expand 0/1 to 0/255 and replicate to RGB with alpha 255.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 3;
                int bitOffset = 7 - (pixelIndex & 7);
                int rawBit = (source[byteIndex] >> bitOffset) & 0x1;
                uint gray = (uint)(rawBit * 255);
                rgbaPtr[pixelIndex] = gray | (gray << 8) | (gray << 16) | 0xFF000000U;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray2(byte* source, byte* destination, int columns)
        {
            // 2-bit grayscale: 4 samples per byte, high bits first. Expand 0..3 to 0..255 by multiplying by 85.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 2; // 4 samples per byte.
                int sampleInByte = pixelIndex & 3; // 0..3.
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (source[byteIndex] >> bitOffset) & 0x3; // 0..3.
                uint gray = (uint)(rawValue * 85); // 255 / 3 = 85.
                rgbaPtr[pixelIndex] = gray | (gray << 8) | (gray << 16) | 0xFF000000U;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray4(byte* source, byte* destination, int columns)
        {
            // 4-bit grayscale: 2 samples per byte. High nibble first. Expand 0..15 to 0..255 by multiplying by 17.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 1; // 2 samples per byte.
                bool highNibble = (pixelIndex & 1) == 0;
                int value = source[byteIndex];
                int rawValue = highNibble ? (value >> 4) : (value & 0xF); // 0..15.
                uint gray = (uint)(rawValue * 17); // 255 / 15 ≈ 17.
                rgbaPtr[pixelIndex] = gray | (gray << 8) | (gray << 16) | 0xFF000000U;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleGray8(byte* source, byte* destination, int columns)
        {
            // Per-pixel pointer expansion mirroring UpsampleScaleRgb8 pattern (no Unsafe.ReadUnaligned usage).
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0, srcOffset = 0; pixelIndex < columns; pixelIndex++, srcOffset++)
            {
                byte gray = source[srcOffset];
                uint g = gray;
                rgbaPtr[pixelIndex] = g | (g << 8) | (g << 16) | 0xFF000000U;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleGray16(byte* source, byte* destination, int columns)
        {
            // Per-pixel pointer expansion mirroring UpsampleScaleRgb8 style (no Unsafe.ReadUnaligned usage).
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0, srcOffset = 0; pixelIndex < columns; pixelIndex++, srcOffset += 2)
            {
                byte highByte = source[srcOffset];
                uint g = highByte;
                rgbaPtr[pixelIndex] = g | (g << 8) | (g << 16) | 0xFF000000U;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb1(byte* source, byte* destination, int columns)
        {
            // Classic per-component bit extraction: 1 bit per component, packed left-to-right (high bit first in each byte).
            // Each pixel consumes 3 bits (R,G,B). We scale 0/1 to 0/255.
            uint* destinationUint = (uint*)destination;
            int totalSamples = columns * 3;
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 3;
                int bitOffset = 7 - (sampleBase & 7);
                int rRaw = (source[byteIndex] >> bitOffset) & 0x1;
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 3;
                bitOffset = 7 - (gSampleIndex & 7);
                int gRaw = (source[byteIndex] >> bitOffset) & 0x1;
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 3;
                bitOffset = 7 - (bSampleIndex & 7);
                int bRaw = (source[byteIndex] >> bitOffset) & 0x1;
                uint rgba = (uint)(rRaw * 255 | (gRaw * 255) << 8 | (bRaw * 255) << 16 | 0xFF000000U);
                destinationUint[columnIndex] = rgba;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb2(byte* source, byte* destination, int columns)
        {
            // Classic per-component bit extraction: 2 bits per component (values 0..3) packed high bits first.
            // Scale 0..3 to 0..255 by multiplying by 85.
            uint* destinationUint = (uint*)destination;
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 2; // 4 samples per byte.
                int twoBitIndex = sampleBase & 3;
                int bitOffset = 6 - (twoBitIndex * 2);
                int rRaw = (source[byteIndex] >> bitOffset) & 0x3;
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 2;
                twoBitIndex = gSampleIndex & 3;
                bitOffset = 6 - (twoBitIndex * 2);
                int gRaw = (source[byteIndex] >> bitOffset) & 0x3;
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 2;
                twoBitIndex = bSampleIndex & 3;
                bitOffset = 6 - (twoBitIndex * 2);
                int bRaw = (source[byteIndex] >> bitOffset) & 0x3;
                uint rgba = (uint)(rRaw * 85 | (gRaw * 85) << 8 | (bRaw * 85) << 16 | 0xFF000000U);
                destinationUint[columnIndex] = rgba;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb4(byte* source, byte* destination, int columns)
        {
            // Classic per-component nibble extraction: 4 bits per component (values 0..15) packed high nibble first.
            // Scale 0..15 to 0..255 by multiplying by 17.
            uint* destinationUint = (uint*)destination;
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 1; // 2 samples per byte.
                bool highNibble = (sampleBase & 1) == 0;
                int value = source[byteIndex];
                int rRaw = highNibble ? (value >> 4) : (value & 0xF);
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 1;
                highNibble = (gSampleIndex & 1) == 0;
                value = source[byteIndex];
                int gRaw = highNibble ? (value >> 4) : (value & 0xF);
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 1;
                highNibble = (bSampleIndex & 1) == 0;
                value = source[byteIndex];
                int bRaw = highNibble ? (value >> 4) : (value & 0xF);
                uint rgba = (uint)(rRaw * 17 | (gRaw * 17) << 8 | (bRaw * 17) << 16 | 0xFF000000U);
                destinationUint[columnIndex] = rgba;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb8(byte* source, byte* destination, int columns)
        {
            for (long i = 0, offsetRgb = 0; i < columns; i++, offsetRgb += 3)
            {
                ((uint*)destination)[i] = *(uint*)(source + offsetRgb) | 0xff000000;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleRgb16(byte* source, byte* destination, int columns)
        {
            // Per-pixel pointer expansion mirroring UpsampleScaleRgb8 style (no Unsafe.ReadUnaligned usage).
            uint* destinationUint = (uint*)destination;
            for (int columnIndex = 0, srcOffset = 0; columnIndex < columns; columnIndex++, srcOffset += 6)
            {
                int r16 = (source[srcOffset] << 8) | source[srcOffset + 1];
                int g16 = (source[srcOffset + 2] << 8) | source[srcOffset + 3];
                int b16 = (source[srcOffset + 4] << 8) | source[srcOffset + 5];
                byte r8 = (byte)(r16 >> 8);
                byte g8 = (byte)(g16 >> 8);
                byte b8 = (byte)(b16 >> 8);
                destinationUint[columnIndex] = (uint)(r8 | (g8 << 8) | (b8 << 16) | 0xFF000000U);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk1(byte* source, byte* destination, int columns)
        {
            // 1-bit CMYK: 4 bits per pixel (C,M,Y,K). Expand each 0/1 to 0/255 and map C->R, M->G, Y->B, K->A.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                int cByte = sampleBase >> 3;
                int cBitOffset = 7 - (sampleBase & 7);
                int cRaw = (source[cByte] >> cBitOffset) & 0x1;
                int mSample = sampleBase + 1;
                int mByte = mSample >> 3;
                int mBitOffset = 7 - (mSample & 7);
                int mRaw = (source[mByte] >> mBitOffset) & 0x1;
                int ySample = sampleBase + 2;
                int yByte = ySample >> 3;
                int yBitOffset = 7 - (ySample & 7);
                int yRaw = (source[yByte] >> yBitOffset) & 0x1;
                int kSample = sampleBase + 3;
                int kByte = kSample >> 3;
                int kBitOffset = 7 - (kSample & 7);
                int kRaw = (source[kByte] >> kBitOffset) & 0x1;
                uint c = (uint)(cRaw * 255);
                uint m = (uint)(mRaw * 255);
                uint y = (uint)(yRaw * 255);
                uint k = (uint)(kRaw * 255);
                rgbaPtr[pixelIndex] = c | (m << 8) | (y << 16) | (k << 24);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk2(byte* source, byte* destination, int columns)
        {
            // 2-bit CMYK: each component 0..3, 4 components per pixel. Expand by multiplying by 85.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                int cByte = sampleBase >> 2;
                int cTwoBitIndex = sampleBase & 3;
                int cBitOffset = 6 - (cTwoBitIndex * 2);
                int cRaw = (source[cByte] >> cBitOffset) & 0x3;
                int mSample = sampleBase + 1;
                int mByte = mSample >> 2;
                int mTwoBitIndex = mSample & 3;
                int mBitOffset = 6 - (mTwoBitIndex * 2);
                int mRaw = (source[mByte] >> mBitOffset) & 0x3;
                int ySample = sampleBase + 2;
                int yByte = ySample >> 2;
                int yTwoBitIndex = ySample & 3;
                int yBitOffset = 6 - (yTwoBitIndex * 2);
                int yRaw = (source[yByte] >> yBitOffset) & 0x3;
                int kSample = sampleBase + 3;
                int kByte = kSample >> 2;
                int kTwoBitIndex = kSample & 3;
                int kBitOffset = 6 - (kTwoBitIndex * 2);
                int kRaw = (source[kByte] >> kBitOffset) & 0x3;
                uint c = (uint)(cRaw * 85);
                uint m = (uint)(mRaw * 85);
                uint y = (uint)(yRaw * 85);
                uint k = (uint)(kRaw * 85);
                rgbaPtr[pixelIndex] = c | (m << 8) | (y << 16) | (k << 24);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk4(byte* source, byte* destination, int columns)
        {
            // 4-bit CMYK: 2 samples per byte, high nibble first. Expand 0..15 to 0..255 by multiplying by 17.
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                // C
                int cByte = sampleBase >> 1;
                bool cHigh = (sampleBase & 1) == 0;
                int cValue = source[cByte];
                int cRaw = cHigh ? (cValue >> 4) : (cValue & 0xF);
                // M
                int mSample = sampleBase + 1;
                int mByte = mSample >> 1;
                bool mHigh = (mSample & 1) == 0;
                int mValue = source[mByte];
                int mRaw = mHigh ? (mValue >> 4) : (mValue & 0xF);
                // Y
                int ySample = sampleBase + 2;
                int yByte = ySample >> 1;
                bool yHigh = (ySample & 1) == 0;
                int yValue = source[yByte];
                int yRaw = yHigh ? (yValue >> 4) : (yValue & 0xF);
                // K
                int kSample = sampleBase + 3;
                int kByte = kSample >> 1;
                bool kHigh = (kSample & 1) == 0;
                int kValue = source[kByte];
                int kRaw = kHigh ? (kValue >> 4) : (kValue & 0xF);
                uint c = (uint)(cRaw * 17);
                uint m = (uint)(mRaw * 17);
                uint y = (uint)(yRaw * 17);
                uint k = (uint)(kRaw * 17);
                rgbaPtr[pixelIndex] = c | (m << 8) | (y << 16) | (k << 24);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleCmyk8(byte* source, byte* destination, int columns)
        {
            System.Buffer.MemoryCopy(source, destination, columns * 4, columns * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleCmyk16(byte* source, byte* destination, int columns)
        {
            uint* rgbaPtr = (uint*)destination;
            for (int pixelIndex = 0, srcOffset = 0; pixelIndex < columns; pixelIndex++, srcOffset += 8)
            {
                byte cHi = source[srcOffset];
                byte mHi = source[srcOffset + 2];
                byte yHi = source[srcOffset + 4];
                byte kHi = source[srcOffset + 6];
                rgbaPtr[pixelIndex] = (uint)(cHi | (mHi << 8) | (yHi << 16) | (kHi << 24));
            }
        }
    }
}
