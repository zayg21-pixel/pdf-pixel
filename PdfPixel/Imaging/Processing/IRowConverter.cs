using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Converts source image rows into normalized 8-bit-per-component output.
/// All implementations produce samples at 8 bits per component regardless
/// of the source bit depth.
/// </summary>
internal interface IRowConverter
{
    bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, int sourceStartBit, Span<byte> destRow);
}
