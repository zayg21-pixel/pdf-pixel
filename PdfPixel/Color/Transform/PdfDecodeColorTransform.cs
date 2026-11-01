using PdfPixel.Objects;
using System;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Maps each component from the range 0 to 1 onto its decode range.
/// </summary>
public sealed class PdfDecodeColorTransform : IPdfColorTransform
{
    private readonly PdfRange[] _decode;
    private readonly float[] _buffer;

    /// <summary>
    /// Initializes a transform mapping each component onto the matching entry of <paramref name="decode"/>.
    /// </summary>
    /// <param name="decode">Decode range of each component.</param>
    public PdfDecodeColorTransform(PdfRange[] decode)
    {
        if (decode == null || decode.Length == 0)
        {
            throw new ArgumentException("At least one decode range is required.", nameof(decode));
        }

        _decode = decode;
        _buffer = new float[decode.Length];
    }

    /// <inheritdoc />
    public int InputComponentCount => _decode.Length;

    /// <inheritdoc />
    public int OutputComponentCount => _decode.Length;

    /// <inheritdoc />
    public ReadOnlySpan<float> Transform(ReadOnlySpan<float> input)
    {
        if (input.Length < _decode.Length)
        {
            throw new ArgumentException("The input holds fewer components than the transform decodes.", nameof(input));
        }

        for (int componentIndex = 0; componentIndex < _decode.Length; componentIndex++)
        {
            _buffer[componentIndex] = _decode[componentIndex].Denormalize(input[componentIndex]);
        }

        return _buffer;
    }
}
