using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Resamples source image rows onto an output grid, producing normalized 8-bit-per-component
/// output regardless of the source bit depth. A source row is offered until it has nothing
/// further to give: each call returning true has written one output row, and false ends the row.
/// A grid coarser than the source consumes several rows before the first true, a finer one
/// returns true several times over, and a matching grid returns true exactly once.
/// </summary>
internal interface IRowConverter
{
    bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, int sourceStartBit, Span<byte> destRow);
}
