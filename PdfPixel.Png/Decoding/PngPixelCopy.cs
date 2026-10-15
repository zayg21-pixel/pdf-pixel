using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Png.Decoding;

/// <summary>
/// Copies single pixels between rows of packed, MSB-first pixels.
/// </summary>
internal static class PngPixelCopy
{
    /// <summary>
    /// Copies pixel <paramref name="sourceIndex"/> of <paramref name="source"/> to pixel
    /// <paramref name="destinationIndex"/> of <paramref name="destination"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(in ReadOnlySpan<byte> source, int sourceIndex, in Span<byte> destination, int destinationIndex, int bitsPerPixel)
    {
        if (bitsPerPixel >= 8)
        {
            int bytesPerPixel = bitsPerPixel >> 3;
            source.Slice(sourceIndex * bytesPerPixel, bytesPerPixel).CopyTo(destination.Slice(destinationIndex * bytesPerPixel, bytesPerPixel));
            return;
        }

        int mask = (1 << bitsPerPixel) - 1;

        int sourceBit = sourceIndex * bitsPerPixel;
        int value = (source[sourceBit >> 3] >> (8 - bitsPerPixel - (sourceBit & 7))) & mask;

        int destinationBit = destinationIndex * bitsPerPixel;
        int shift = 8 - bitsPerPixel - (destinationBit & 7);
        ref byte target = ref destination[destinationBit >> 3];
        target = (byte)((target & ~(mask << shift)) | (value << shift));
    }
}
