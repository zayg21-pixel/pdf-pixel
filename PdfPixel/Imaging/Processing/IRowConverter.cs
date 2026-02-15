using System;

namespace PdfPixel.Imaging.Processing;

internal interface IRowConverter
{
    int BitsPerComponent { get; }
    bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, Span<byte> destRow);
}
