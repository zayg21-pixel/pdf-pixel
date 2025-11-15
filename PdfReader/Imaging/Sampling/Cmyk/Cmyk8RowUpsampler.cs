using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Cmyk;

/// <summary>
/// Upsamples a row of 8-bit CMYK pixels into RGBA format.
/// </summary>
internal sealed class Cmyk8RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Cmyk8RowUpsampler(int columns)
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
            uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, columnIndex * 4));
            rgbDestination = Unsafe.As<uint, Rgba>(ref value);
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
