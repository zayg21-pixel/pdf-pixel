using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Gray;

/// <summary>
/// Upsamples a row of 4-bit grayscale pixels into 8-bit grayscale values.
/// </summary>
internal sealed class Gray4RowUpsampler : IRowUpsampler
{
    private readonly int _columns;
    private readonly bool _upscale;
    private readonly int _scaleFactor;

    public Gray4RowUpsampler(int columns, bool upscale)
    {
        _columns = columns;
        _upscale = upscale;
        _scaleFactor = _upscale ? 17 : 1;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
        {
            int byteIndex = pixelIndex >> 1;
            bool highNibble = (pixelIndex & 1) == 0;
            int value = Unsafe.Add(ref source, byteIndex);
            int rawValue = highNibble ? value >> 4 : value & 0xF;
            byte grayValue = (byte)(rawValue * _scaleFactor);
            Unsafe.Add(ref destination, pixelIndex) = grayValue;
        }
    }
}
