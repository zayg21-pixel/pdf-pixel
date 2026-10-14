using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Serves the alpha plane rows an image is composited with, on the output grid its rows are
/// resampled to.
/// </summary>
internal interface IAlphaRowSource
{
    /// <summary>
    /// Returns the alpha values for the current output row of <paramref name="target"/>, one byte
    /// per pixel of that row, or an empty span when the row cannot be served.
    /// </summary>
    ReadOnlySpan<byte> GetRow(PdfImageRowTarget target);
}
