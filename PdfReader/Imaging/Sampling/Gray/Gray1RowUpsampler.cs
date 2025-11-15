using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Gray;

/// <summary>
/// Upsamples a row of 1-bit grayscale pixels into 8-bit grayscale values.
/// </summary>
internal sealed class Gray1RowUpsampler : IRowUpsampler
{
    private readonly int _columns;
    private readonly bool _upscale;
    private readonly int _scaleFactor;

    public Gray1RowUpsampler(int columns, bool upscale)
    {
        _columns = columns;
        _upscale = upscale;
        _scaleFactor = _upscale ? 255 : 1;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
        {
            int byteIndex = pixelIndex >> 3;
            int bitOffset = 7 - (pixelIndex & 7);
            int rawBit = Unsafe.Add(ref source, byteIndex) >> bitOffset & 0x1;
            byte grayValue = (byte)(rawBit * _scaleFactor);
            Unsafe.Add(ref destination, pixelIndex) = grayValue;
        }
    }
}
