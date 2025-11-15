using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Gray;

/// <summary>
/// Upsamples a row of 2-bit grayscale pixels into 8-bit grayscale values.
/// </summary>
internal sealed class Gray2RowUpsampler : IRowUpsampler
{
    private readonly int _columns;
    private readonly bool _upscale;
    private readonly int _scaleFactor;

    public Gray2RowUpsampler(int columns, bool upscale)
    {
        _columns = columns;
        _upscale = upscale;
        _scaleFactor = _upscale ? 85 : 1;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
        {
            int byteIndex = pixelIndex >> 2;
            int sampleInByte = pixelIndex & 3;
            int bitOffset = 6 - sampleInByte * 2;
            int rawValue = Unsafe.Add(ref source, byteIndex) >> bitOffset & 0x3;
            byte grayValue = (byte)(rawValue * _scaleFactor);
            Unsafe.Add(ref destination, pixelIndex) = grayValue;
        }
    }
}
