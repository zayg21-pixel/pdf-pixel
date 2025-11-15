using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb;

/// <summary>
/// Upsamples a row of 2-bit grayscale pixels into RGBA format.
/// </summary>
internal sealed class GrayRgba2RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public GrayRgba2RowUpsampler(int columns)
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
            int byteIndex = columnIndex >> 2;
            int sampleInByte = columnIndex & 3;
            int bitOffset = 6 - sampleInByte * 2;
            int rawValue = Unsafe.Add(ref source, byteIndex) >> bitOffset & 0x3;
            byte gray = (byte)(rawValue * 85);
            rgbDestination.R = gray;
            rgbDestination.G = gray;
            rgbDestination.B = gray;
            rgbDestination.A = 255;

            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
