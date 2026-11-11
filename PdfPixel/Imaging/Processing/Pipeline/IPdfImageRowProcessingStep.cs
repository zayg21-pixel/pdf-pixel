using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// One step of an image row processing pipeline. The output may start where the input starts, both in the one buffer
/// of the pipeline: a step reads every input value before writing over it, walking a row that grows from its end and
/// a row that shrinks from its start.
/// </summary>
public interface IPdfImageRowProcessingStep
{
    /// <summary>
    /// Returns the number of bytes the step writes for an input row of <paramref name="inputLength"/> bytes.
    /// </summary>
    /// <param name="inputLength">Number of bytes of the input row.</param>
    int GetOutputLength(int inputLength);

    /// <summary>
    /// Returns the row the next <see cref="Process(ReadOnlySpan{byte}, Span{byte}, int)"/> call writes for the
    /// input row at <paramref name="inputRowIndex"/>.
    /// </summary>
    /// <param name="inputRowIndex">Index of the input row.</param>
    PdfImageOutputRow GetOutputRow(int inputRowIndex);

    /// <summary>
    /// Processes the input row at <paramref name="inputRowIndex"/>, writing the row <see cref="GetOutputRow"/>
    /// returns.
    /// </summary>
    /// <param name="input">Bytes of the input row, or empty to write the next row of the previous input.</param>
    /// <param name="output">Receives the row.</param>
    /// <param name="inputRowIndex">Index of the input row.</param>
    void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex);

    /// <summary>
    /// Processes an input row whose index the step does not need.
    /// </summary>
    /// <param name="input">Bytes of the input row.</param>
    /// <param name="output">Receives the row.</param>
    /// <exception cref="NotSupportedException">The step needs the row index.</exception>
    void Process(ReadOnlySpan<byte> input, Span<byte> output);
}
