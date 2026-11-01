using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// One step of an image row processing pipeline.
/// </summary>
public interface IPdfImageRowProcessingStep
{
    /// <summary>
    /// Processes the input row at <paramref name="inputRowIndex"/>.
    /// </summary>
    /// <param name="input">Bytes of the input row.</param>
    /// <param name="inputRowIndex">Index of the input row.</param>
    /// <returns>The produced row, or an empty row when the input produces none.</returns>
    PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex);
}
