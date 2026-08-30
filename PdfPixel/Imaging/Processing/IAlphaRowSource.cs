using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Serves the alpha plane rows an image is composited with, on the output grid its rows are
/// resampled to.
/// </summary>
internal interface IAlphaRowSource
{
    /// <summary>
    /// Returns the alpha values for <paramref name="outputRowIndex"/> of the output grid, one byte
    /// per pixel, or an empty span when the row cannot be served.
    /// </summary>
    ReadOnlySpan<byte> GetRow(int outputRowIndex);
}
