using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Maps each component from the range 0 to 1 onto its decode range.
/// </summary>
public sealed class PdfDecodeColorTransform : IPdfColorTransform
{
    private readonly PdfRange[] _decode;

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
    }

    /// <inheritdoc />
    public int InputComponentCount => _decode.Length;

    /// <inheritdoc />
    public int OutputComponentCount => _decode.Length;

    /// <inheritdoc />
    public void Transform(ref float input, ref float output)
    {
        for (int componentIndex = 0; componentIndex < _decode.Length; componentIndex++)
        {
            Unsafe.Add(ref output, componentIndex) = _decode[componentIndex].Denormalize(Unsafe.Add(ref input, componentIndex));
        }
    }
}
