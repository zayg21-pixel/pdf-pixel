using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Gray;

/// <summary>
/// Upsamples a row of 8-bit grayscale pixels into 8-bit grayscale values.
/// </summary>
internal sealed class Gray8RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Gray8RowUpsampler(int columns)
    {
        _columns = columns;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(ref byte source, ref byte destination)
    {
        for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
        {
            byte grayValue = Unsafe.Add(ref source, pixelIndex);
            Unsafe.Add(ref destination, pixelIndex) = grayValue;
        }
    }
}
