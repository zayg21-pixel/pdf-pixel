using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb;

/// <summary>
/// Upsamples a row of 8-bit grayscale pixels into RGBA format.
/// </summary>
internal sealed class GrayRgba8RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public GrayRgba8RowUpsampler(int columns)
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
            byte gray = Unsafe.Add(ref source, columnIndex);
            rgbDestination.R = gray;
            rgbDestination.G = gray;
            rgbDestination.B = gray;
            rgbDestination.A = 255;
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
