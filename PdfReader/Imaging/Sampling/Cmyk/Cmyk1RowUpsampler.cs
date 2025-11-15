using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Cmyk;

/// <summary>
/// Upsamples a row of 1-bit CMYK pixels into RGBA format.
/// </summary>
internal sealed class Cmyk1RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Cmyk1RowUpsampler(int columns)
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
            int sampleBase = columnIndex * 4;
            int cByteIndex = sampleBase >> 3;
            int cBitOffset = 7 - (sampleBase & 7);
            int cRaw = Unsafe.Add(ref source, cByteIndex) >> cBitOffset & 0x1;
            int mSampleIndex = sampleBase + 1;
            int mByteIndex = mSampleIndex >> 3;
            int mBitOffset = 7 - (mSampleIndex & 7);
            int mRaw = Unsafe.Add(ref source, mByteIndex) >> mBitOffset & 0x1;
            int ySampleIndex = sampleBase + 2;
            int yByteIndex = ySampleIndex >> 3;
            int yBitOffset = 7 - (ySampleIndex & 7);
            int yRaw = Unsafe.Add(ref source, yByteIndex) >> yBitOffset & 0x1;
            int kSampleIndex = sampleBase + 3;
            int kByteIndex = kSampleIndex >> 3;
            int kBitOffset = 7 - (kSampleIndex & 7);
            int kRaw = Unsafe.Add(ref source, kByteIndex) >> kBitOffset & 0x1;
            rgbDestination.R = (byte)(cRaw * 255);
            rgbDestination.G = (byte)(mRaw * 255);
            rgbDestination.B = (byte)(yRaw * 255);
            rgbDestination.A = (byte)(kRaw * 255);
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
