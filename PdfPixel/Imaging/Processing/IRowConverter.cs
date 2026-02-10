using System;

namespace PdfPixel.Imaging.Processing;

internal interface IRowConverter
{
    bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, Span<byte> destRow);
}
