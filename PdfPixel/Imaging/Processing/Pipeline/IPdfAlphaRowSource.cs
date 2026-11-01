using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Serves the alpha rows a <see cref="PdfImageRowAlphaPlaneStep"/> merges into its rows.
/// </summary>
public interface IPdfAlphaRowSource
{
    /// <summary>
    /// Returns the alpha values of the row at <paramref name="rowIndex"/>, one float in the range 0 to 1
    /// per pixel, or an empty span when the row cannot be served.
    /// </summary>
    ReadOnlySpan<float> GetRow(int rowIndex);
}
