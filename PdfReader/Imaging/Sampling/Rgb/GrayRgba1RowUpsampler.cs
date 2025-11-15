using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb;

/// <summary>
/// Upsamples a row of 1-bit grayscale pixels into RGBA format.
/// </summary>
internal sealed class GrayRgba1RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public GrayRgba1RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        ref var rgbDestination = ref Unsafe.As<byte, RgbaPacked>(ref destination);

        for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
        {
            int byteIndex = columnIndex >> 3;
            int bitOffset = 7 - (columnIndex & 7);
            int rawBit = Unsafe.Add(ref source, byteIndex) >> bitOffset & 0x1;
            byte gray = (byte)(rawBit * 255);
            rgbDestination.R = gray;
            rgbDestination.G = gray;
            rgbDestination.B = gray;
            rgbDestination.A = 255;

            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
