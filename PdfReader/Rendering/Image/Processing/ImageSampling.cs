using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    internal static class ImageSampling
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRowByteLength(int width, int components, int bitsPerComponent)
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
        public static unsafe int ReadSampleRaw(byte* decoded, int rowIndex, int columnIndex, int componentIndex, int width, int components, int bitsPerComponent, int rowByteLength)
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
    }
}
