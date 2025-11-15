using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Cmyk;

/// <summary>
/// Upsamples a row of 2-bit CMYK pixels into RGBA format.
/// </summary>
internal sealed class Cmyk2RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Cmyk2RowUpsampler(int columns)
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
            int cByteIndex = sampleBase >> 2;
            int cTwoBitIndex = sampleBase & 3;
            int cBitOffset = 6 - cTwoBitIndex * 2;
            int cRaw = Unsafe.Add(ref source, cByteIndex) >> cBitOffset & 0x3;
            int mSampleIndex = sampleBase + 1;
            int mByteIndex = mSampleIndex >> 2;
            int mTwoBitIndex = mSampleIndex & 3;
            int mBitOffset = 6 - mTwoBitIndex * 2;
            int mRaw = Unsafe.Add(ref source, mByteIndex) >> mBitOffset & 0x3;
            int ySampleIndex = sampleBase + 2;
            int yByteIndex = ySampleIndex >> 2;
            int yTwoBitIndex = ySampleIndex & 3;
            int yBitOffset = 6 - yTwoBitIndex * 2;
            int yRaw = Unsafe.Add(ref source, yByteIndex) >> yBitOffset & 0x3;
            int kSampleIndex = sampleBase + 3;
            int kByteIndex = kSampleIndex >> 2;
            int kTwoBitIndex = kSampleIndex & 3;
            int kBitOffset = 6 - kTwoBitIndex * 2;
            int kRaw = Unsafe.Add(ref source, kByteIndex) >> kBitOffset & 0x3;
            rgbDestination.R = (byte)(cRaw * 85);
            rgbDestination.G = (byte)(mRaw * 85);
            rgbDestination.B = (byte)(yRaw * 85);
            rgbDestination.A = (byte)(kRaw * 85);
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
