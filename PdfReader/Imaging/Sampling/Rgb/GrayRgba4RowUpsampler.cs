using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb;

/// <summary>
/// Upsamples a row of 4-bit grayscale pixels into RGBA format.
/// </summary>
internal sealed class GrayRgba4RowUpsampler : IRowUpsampler
{
    private readonly int _columns;
    private const int NibbleScaleFactor = 17;

    public GrayRgba4RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

        for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
        {
            int byteIndex = columnIndex >> 1;
            bool isHighNibble = (columnIndex & 1) == 0;
            int value = Unsafe.Add(ref source, byteIndex);
            int rawValue = isHighNibble ? value >> 4 : value & 0xF;
            byte gray = (byte)(rawValue * NibbleScaleFactor);
            rgbDestination.R = gray;
            rgbDestination.G = gray;
            rgbDestination.B = gray;
            rgbDestination.A = 255;
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
