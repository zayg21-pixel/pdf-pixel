using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Cmyk;

/// <summary>
/// Upsamples a row of 4-bit CMYK pixels into RGBA format.
/// </summary>
internal sealed class Cmyk4RowUpsampler : IRowUpsampler
{
    private readonly int _columns;

    public Cmyk4RowUpsampler(int columns)
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
            int cByteIndex = sampleBase >> 1;
            bool cHigh = (sampleBase & 1) == 0;
            int cValue = Unsafe.Add(ref source, cByteIndex);
            int cRaw = cHigh ? cValue >> 4 : cValue & 0xF;
            int mSampleIndex = sampleBase + 1;
            int mByteIndex = mSampleIndex >> 1;
            bool mHigh = (mSampleIndex & 1) == 0;
            int mValue = Unsafe.Add(ref source, mByteIndex);
            int mRaw = mHigh ? mValue >> 4 : mValue & 0xF;
            int ySampleIndex = sampleBase + 2;
            int yByteIndex = ySampleIndex >> 1;
            bool yHigh = (ySampleIndex & 1) == 0;
            int yValue = Unsafe.Add(ref source, yByteIndex);
            int yRaw = yHigh ? yValue >> 4 : yValue & 0xF;
            int kSampleIndex = sampleBase + 3;
            int kByteIndex = kSampleIndex >> 1;
            bool kHigh = (kSampleIndex & 1) == 0;
            int kValue = Unsafe.Add(ref source, kByteIndex);
            int kRaw = kHigh ? kValue >> 4 : kValue & 0xF;
            rgbDestination.R = (byte)(cRaw * 17);
            rgbDestination.G = (byte)(mRaw * 17);
            rgbDestination.B = (byte)(yRaw * 17);
            rgbDestination.A = (byte)(kRaw * 17);
            rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
        }
    }
}
