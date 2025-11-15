using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Cmyk;

/// <summary>
/// Upsamples a row of 16-bit CMYK pixels into RGBA format.
/// </summary>
internal sealed class Cmyk16RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Cmyk16RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        // Writes raw 16-bit CMYK values for each pixel, allowing later color management (e.g., ICC profile conversion).
        for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
        {
            int sourceOffset = columnIndex * 4 * 2; // 4 components, 2 bytes each
            int destinationOffset = columnIndex * 4 * 2; // 4 components, 2 bytes each

            ushort cyan = (ushort)(Unsafe.Add(ref source, sourceOffset) | Unsafe.Add(ref source, sourceOffset + 1) << 8);
            ushort magenta = (ushort)(Unsafe.Add(ref source, sourceOffset + 2) | Unsafe.Add(ref source, sourceOffset + 3) << 8);
            ushort yellow = (ushort)(Unsafe.Add(ref source, sourceOffset + 4) | Unsafe.Add(ref source, sourceOffset + 5) << 8);
            ushort black = (ushort)(Unsafe.Add(ref source, sourceOffset + 6) | Unsafe.Add(ref source, sourceOffset + 7) << 8);

            Unsafe.Add(ref destination, destinationOffset) = (byte)(cyan & 0xFF);
            Unsafe.Add(ref destination, destinationOffset + 1) = (byte)(cyan >> 8);
            Unsafe.Add(ref destination, destinationOffset + 2) = (byte)(magenta & 0xFF);
            Unsafe.Add(ref destination, destinationOffset + 3) = (byte)(magenta >> 8);
            Unsafe.Add(ref destination, destinationOffset + 4) = (byte)(yellow & 0xFF);
            Unsafe.Add(ref destination, destinationOffset + 5) = (byte)(yellow >> 8);
            Unsafe.Add(ref destination, destinationOffset + 6) = (byte)(black & 0xFF);
            Unsafe.Add(ref destination, destinationOffset + 7) = (byte)(black >> 8);
        }
    }
}
