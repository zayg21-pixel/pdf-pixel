using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Gray;

/// <summary>
/// Upsamples a row of 16-bit grayscale pixels into 8-bit grayscale values (high byte only).
/// </summary>
internal sealed class Gray16RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Gray16RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int pixelIndex = 0, sourceOffset = 0; pixelIndex < _columns; pixelIndex++, sourceOffset += 2)
        {
            byte grayValue = Unsafe.Add(ref source, sourceOffset);
            Unsafe.Add(ref destination, pixelIndex) = grayValue;
        }
    }
}
