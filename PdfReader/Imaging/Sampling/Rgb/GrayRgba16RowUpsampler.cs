using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb;

/// <summary>
/// Upsamples a row of 16-bit grayscale pixels into RGBA format (writes full 16 bits per channel).
/// </summary>
internal sealed class GrayRgba16RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public GrayRgba16RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
        {
            int srcOffset = columnIndex * 2;
            int dstOffset = columnIndex * 4 * 2;

            ushort gray = (ushort)(Unsafe.Add(ref source, srcOffset) | Unsafe.Add(ref source, srcOffset + 1) << 8);
            ushort alpha = 65535;

            Unsafe.Add(ref destination, dstOffset) = (byte)(gray & 0xFF);         // R low
            Unsafe.Add(ref destination, dstOffset + 1) = (byte)(gray >> 8);      // R high
            Unsafe.Add(ref destination, dstOffset + 2) = (byte)(gray & 0xFF);    // G low
            Unsafe.Add(ref destination, dstOffset + 3) = (byte)(gray >> 8);      // G high
            Unsafe.Add(ref destination, dstOffset + 4) = (byte)(gray & 0xFF);    // B low
            Unsafe.Add(ref destination, dstOffset + 5) = (byte)(gray >> 8);      // B high
            Unsafe.Add(ref destination, dstOffset + 6) = (byte)(alpha & 0xFF);   // A low
            Unsafe.Add(ref destination, dstOffset + 7) = (byte)(alpha >> 8);     // A high
        }
    }
}
