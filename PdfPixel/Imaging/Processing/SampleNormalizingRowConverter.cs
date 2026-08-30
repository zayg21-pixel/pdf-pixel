using PdfPixel.Parsing;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Row converter that normalizes packed sample values of any bit depth to 8 bits per component
/// without spatial resampling. Every source row produces exactly one output row.
/// </summary>
internal sealed class SampleNormalizingRowConverter : IRowConverter
{
    private readonly int _totalSamples;
    private readonly int _sourceBitsPerComponent;
    private readonly float _scale;

    private int _convertedRowIndex = -1;

    public SampleNormalizingRowConverter(int components, int sourceBitsPerComponent, int width)
    {
        _totalSamples = width * components;
        _sourceBitsPerComponent = sourceBitsPerComponent;

        uint maxCode = (1u << sourceBitsPerComponent) - 1u;
        _scale = (maxCode == 0) ? 0f : 255f / maxCode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, int sourceStartBit, Span<byte> destRow)
    {
        if (_convertedRowIndex == rowIndex)
        {
            return false;
        }

        _convertedRowIndex = rowIndex;

        if (_sourceBitsPerComponent == 8)
        {
            sourceRow.Slice(sourceStartBit >> 3, _totalSamples).CopyTo(destRow);
            return true;
        }

        UintBitReaderFixedLength reader = new(sourceRow, _sourceBitsPerComponent, sourceStartBit);
        UintBitWriter writer = new(destRow);

        for (int i = 0; i < _totalSamples; i++)
        {
            writer.Write8Bits((byte)(reader.Read() * _scale));
        }

        writer.Flush();
        return true;
    }
}
