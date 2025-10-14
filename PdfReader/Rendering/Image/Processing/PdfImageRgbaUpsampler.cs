using System;
using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    internal static class PdfImageRgbaUpsampler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadRawSample(ref byte row, int sampleIndex, int bitsPerComponent)
        {
            switch (bitsPerComponent)
            {
                case 16:
                    {
                        int byteIndex = sampleIndex * 2;
                        int hi = Unsafe.Add(ref row, byteIndex);
                        int lo = Unsafe.Add(ref row, byteIndex + 1);
                        return (hi << 8) | lo;
                    }
                case 8:
                    {
                        return Unsafe.Add(ref row, sampleIndex);
                    }
                case 4:
                    {
                        int byteIndex = sampleIndex >> 1;
                        bool highNibble = (sampleIndex & 1) == 0;
                        int value = Unsafe.Add(ref row, byteIndex);
                        return highNibble ? (value >> 4) & 0x0F : value & 0x0F;
                    }
                case 2:
                    {
                        int byteIndex = sampleIndex >> 2;
                        int shift = 6 - ((sampleIndex & 3) * 2);
                        return (Unsafe.Add(ref row, byteIndex) >> shift) & 0x03;
                    }
                case 1:
                    {
                        int byteIndex = sampleIndex >> 3;
                        int shift = 7 - (sampleIndex & 7);
                        return (Unsafe.Add(ref row, byteIndex) >> shift) & 0x01;
                    }
                default:
                    {
                        return 0;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpsampleScaleRgbaRow(ref byte source, ref Rgba destination, int columns, int components, int bitsPerComponent, PdfPixelProcessor processor)
        {
            switch (components)
            {
                case 1:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleGray1(ref source, ref destination, columns, processor);
                            break;
                        case 2:
                            UpsampleScaleGray2(ref source, ref destination, columns, processor);
                            break;
                        case 4:
                            UpsampleScaleGray4(ref source, ref destination, columns, processor);
                            break;
                        case 8:
                            UpsampleScaleGray8(ref source, ref destination, columns, processor);
                            break;
                        case 16:
                            UpsampleScaleGray16(ref source, ref destination, columns, processor);
                            break;
                    }
                    break;
                case 3:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleRgb1(ref source, ref destination, columns, processor);
                            break;
                        case 2:
                            UpsampleScaleRgb2(ref source, ref destination, columns, processor);
                            break;
                        case 4:
                            UpsampleScaleRgb4(ref source, ref destination, columns, processor);
                            break;
                        case 8:
                            UpsampleScaleRgb8(ref source, ref destination, columns, processor);
                            break;
                        case 16:
                            UpsampleScaleRgb16(ref source, ref destination, columns, processor);
                            break;
                    }
                    break;
                case 4:
                    switch (bitsPerComponent)
                    {
                        case 1:
                            UpsampleScaleCmyk1(ref source, ref destination, columns, processor);
                            break;
                        case 2:
                            UpsampleScaleCmyk2(ref source, ref destination, columns, processor);
                            break;
                        case 4:
                            UpsampleScaleCmyk4(ref source, ref destination, columns, processor);
                            break;
                        case 8:
                            UpsampleScaleCmyk8(ref source, ref destination, columns, processor);
                            break;
                        case 16:
                            UpsampleScaleCmyk16(ref source, ref destination, columns, processor);
                            break;
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray1(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            // 1-bit grayscale: each bit is one pixel (highest bit first). Expand 0/1 to 0/255 and replicate to RGB with alpha 255.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 3;
                int bitOffset = 7 - (pixelIndex & 7);
                int rawBit = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                byte gray = (byte)(rawBit * 255);
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = gray;
                destPixel.G = gray;
                destPixel.B = gray;
                destPixel.A = 255;
                processor.ExecuteGray(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray2(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            // 2-bit grayscale: 4 samples per byte, high bits first. Expand 0..3 to 0..255 by multiplying by 85.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 2; // 4 samples per byte.
                int sampleInByte = pixelIndex & 3; // 0..3.
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3; // 0..3.
                byte gray = (byte)(rawValue * 85); // 255 / 3 = 85.
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = gray;
                destPixel.G = gray;
                destPixel.B = gray;
                destPixel.A = 255;
                processor.ExecuteGray(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray4(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            // 4-bit grayscale: 2 samples per byte. High nibble first. Expand 0..15 to 0..255 by multiplying by 17.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 1; // 2 samples per byte.
                bool highNibble = (pixelIndex & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rawValue = highNibble ? (value >> 4) : (value & 0xF); // 0..15.
                byte gray = (byte)(rawValue * 17); // 255 / 15 ≈ 17.
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = gray;
                destPixel.G = gray;
                destPixel.B = gray;
                destPixel.A = 255;
                processor.ExecuteGray(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray8(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            ref byte sourceByte = ref source;
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                destination.R = sourceByte;
                destination.G = sourceByte;
                destination.B = sourceByte;
                destination.A = 255;
                processor.ExecuteGray(ref destination);
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray16(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int pixelIndex = 0, sourceOffset = 0; pixelIndex < columns; pixelIndex++, sourceOffset += 2)
            {
                byte highByte = Unsafe.Add(ref source, sourceOffset);
                destination.R = highByte;
                destination.G = highByte;
                destination.B = highByte;
                destination.A = 255;
                processor.ExecuteGray(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleRgb1(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 3;
                int bitOffset = 7 - (sampleBase & 7);
                int rRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 3;
                bitOffset = 7 - (gSampleIndex & 7);
                int gRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 3;
                bitOffset = 7 - (bSampleIndex & 7);
                int bRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                ref Rgba destPixel = ref Unsafe.Add(ref destination, columnIndex);
                destPixel.R = (byte)(rRaw * 255);
                destPixel.G = (byte)(gRaw * 255);
                destPixel.B = (byte)(bRaw * 255);
                destPixel.A = 255;
                processor.ExecuteRgba(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleRgb2(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 2; // 4 samples per byte.
                int twoBitIndex = sampleBase & 3;
                int bitOffset = 6 - (twoBitIndex * 2);
                int rRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 2;
                twoBitIndex = gSampleIndex & 3;
                bitOffset = 6 - (twoBitIndex * 2);
                int gRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 2;
                twoBitIndex = bSampleIndex & 3;
                bitOffset = 6 - (twoBitIndex * 2);
                int bRaw = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                ref Rgba destPixel = ref Unsafe.Add(ref destination, columnIndex);
                destPixel.R = (byte)(rRaw * 85);
                destPixel.G = (byte)(gRaw * 85);
                destPixel.B = (byte)(bRaw * 85);
                destPixel.A = 255;
                processor.ExecuteRgba(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleRgb4(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R component.
                int byteIndex = sampleBase >> 1; // 2 samples per byte.
                bool highNibble = (sampleBase & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rRaw = highNibble ? (value >> 4) : (value & 0xF);
                // G component.
                int gSampleIndex = sampleBase + 1;
                byteIndex = gSampleIndex >> 1;
                highNibble = (gSampleIndex & 1) == 0;
                value = Unsafe.Add(ref source, byteIndex);
                int gRaw = highNibble ? (value >> 4) : (value & 0xF);
                // B component.
                int bSampleIndex = sampleBase + 2;
                byteIndex = bSampleIndex >> 1;
                highNibble = (bSampleIndex & 1) == 0;
                value = Unsafe.Add(ref source, byteIndex);
                int bRaw = highNibble ? (value >> 4) : (value & 0xF);
                ref Rgba destPixel = ref Unsafe.Add(ref destination, columnIndex);
                destPixel.R = (byte)(rRaw * 17);
                destPixel.G = (byte)(gRaw * 17);
                destPixel.B = (byte)(bRaw * 17);
                destPixel.A = 255;
                processor.ExecuteRgba(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleRgb8(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            ref Rgb sourcePixel = ref Unsafe.As<byte, Rgb>(ref source);
            for (int columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                destination = Unsafe.As<Rgb, Rgba>(ref sourcePixel);
                destination.A = 255;
                processor.ExecuteRgba(ref destination);
                sourcePixel = ref Unsafe.Add(ref sourcePixel, 1);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleRgb16(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int columnIndex = 0, sourceOffset = 0; columnIndex < columns; columnIndex++, sourceOffset += 6)
            {
                int r16 = (Unsafe.Add(ref source, sourceOffset) << 8) | Unsafe.Add(ref source, sourceOffset + 1);
                int g16 = (Unsafe.Add(ref source, sourceOffset + 2) << 8) | Unsafe.Add(ref source, sourceOffset + 3);
                int b16 = (Unsafe.Add(ref source, sourceOffset + 4) << 8) | Unsafe.Add(ref source, sourceOffset + 5);
                destination.R = (byte)(r16 >> 8);
                destination.G = (byte)(g16 >> 8);
                destination.B = (byte)(b16 >> 8);
                destination.A = 255;
                processor.ExecuteRgba(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk1(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                int cByte = sampleBase >> 3;
                int cBitOffset = 7 - (sampleBase & 7);
                int cRaw = (Unsafe.Add(ref source, cByte) >> cBitOffset) & 0x1;
                int mSample = sampleBase + 1;
                int mByte = mSample >> 3;
                int mBitOffset = 7 - (mSample & 7);
                int mRaw = (Unsafe.Add(ref source, mByte) >> mBitOffset) & 0x1;
                int ySample = sampleBase + 2;
                int yByte = ySample >> 3;
                int yBitOffset = 7 - (ySample & 7);
                int yRaw = (Unsafe.Add(ref source, yByte) >> yBitOffset) & 0x1;
                int kSample = sampleBase + 3;
                int kByte = kSample >> 3;
                int kBitOffset = 7 - (kSample & 7);
                int kRaw = (Unsafe.Add(ref source, kByte) >> kBitOffset) & 0x1;
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = (byte)(cRaw * 255);
                destPixel.G = (byte)(mRaw * 255);
                destPixel.B = (byte)(yRaw * 255);
                destPixel.A = (byte)(kRaw * 255);
                processor.ExecuteCmyk(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk2(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                int cByte = sampleBase >> 2;
                int cTwoBitIndex = sampleBase & 3;
                int cBitOffset = 6 - (cTwoBitIndex * 2);
                int cRaw = (Unsafe.Add(ref source, cByte) >> cBitOffset) & 0x3;
                int mSample = sampleBase + 1;
                int mByte = mSample >> 2;
                int mTwoBitIndex = mSample & 3;
                int mBitOffset = 6 - (mTwoBitIndex * 2);
                int mRaw = (Unsafe.Add(ref source, mByte) >> mBitOffset) & 0x3;
                int ySample = sampleBase + 2;
                int yByte = ySample >> 2;
                int yTwoBitIndex = ySample & 3;
                int yBitOffset = 6 - (yTwoBitIndex * 2);
                int yRaw = (Unsafe.Add(ref source, yByte) >> yBitOffset) & 0x3;
                int kSample = sampleBase + 3;
                int kByte = kSample >> 2;
                int kTwoBitIndex = kSample & 3;
                int kBitOffset = 6 - (kTwoBitIndex * 2);
                int kRaw = (Unsafe.Add(ref source, kByte) >> kBitOffset) & 0x3;
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = (byte)(cRaw * 85);
                destPixel.G = (byte)(mRaw * 85);
                destPixel.B = (byte)(yRaw * 85);
                destPixel.A = (byte)(kRaw * 85);
                processor.ExecuteCmyk(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk4(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int sampleBase = pixelIndex * 4;
                // C
                int cByte = sampleBase >> 1;
                bool cHigh = (sampleBase & 1) == 0;
                int cValue = Unsafe.Add(ref source, cByte);
                int cRaw = cHigh ? (cValue >> 4) : (cValue & 0xF);
                // M
                int mSample = sampleBase + 1;
                int mByte = mSample >> 1;
                bool mHigh = (mSample & 1) == 0;
                int mValue = Unsafe.Add(ref source, mByte);
                int mRaw = mHigh ? (mValue >> 4) : (mValue & 0xF);
                // Y
                int ySample = sampleBase + 2;
                int yByte = ySample >> 1;
                bool yHigh = (ySample & 1) == 0;
                int yValue = Unsafe.Add(ref source, yByte);
                int yRaw = yHigh ? (yValue >> 4) : (yValue & 0xF);
                // K
                int kSample = sampleBase + 3;
                int kByte = kSample >> 1;
                bool kHigh = (kSample & 1) == 0;
                int kValue = Unsafe.Add(ref source, kByte);
                int kRaw = kHigh ? (kValue >> 4) : (kValue & 0xF);
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = (byte)(cRaw * 17);
                destPixel.G = (byte)(mRaw * 17);
                destPixel.B = (byte)(yRaw * 17);
                destPixel.A = (byte)(kRaw * 17);
                processor.ExecuteCmyk(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk8(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int i = 0; i < columns; i++)
            {
                uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, i * 4));
                ref Rgba destPixel = ref Unsafe.Add(ref destination, i);
                destPixel = Unsafe.As<uint, Rgba>(ref value);
                processor.ExecuteCmyk(ref destPixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleCmyk16(ref byte source, ref Rgba destination, int columns, PdfPixelProcessor processor)
        {
            for (int pixelIndex = 0, srcOffset = 0; pixelIndex < columns; pixelIndex++, srcOffset += 8)
            {
                byte cHi = Unsafe.Add(ref source, srcOffset);
                byte mHi = Unsafe.Add(ref source, srcOffset + 2);
                byte yHi = Unsafe.Add(ref source, srcOffset + 4);
                byte kHi = Unsafe.Add(ref source, srcOffset + 6);
                ref Rgba destPixel = ref Unsafe.Add(ref destination, pixelIndex);
                destPixel.R = cHi;
                destPixel.G = mHi;
                destPixel.B = yHi;
                destPixel.A = kHi;
                processor.ExecuteCmyk(ref destPixel);
            }
        }
    }
}
