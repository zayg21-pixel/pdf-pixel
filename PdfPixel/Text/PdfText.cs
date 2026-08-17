using System;
using PdfPixel.Models;

namespace PdfPixel.Text;

/// <summary>
/// Represents the undecoded bytes of a PDF text-showing operand.
/// </summary>
public readonly struct PdfText
{
    /// <summary>
    /// Initializes a new <see cref="PdfText"/> with the raw PDF character code bytes.
    /// </summary>
    public PdfText(in ReadOnlyMemory<byte> rawBytes) => RawBytes = rawBytes;

    /// <summary>
    /// Gets the raw character code bytes as they appear in the PDF.
    /// </summary>
    public ReadOnlyMemory<byte> RawBytes { get; }

    /// <summary>
    /// Gets whether there are no bytes.
    /// </summary>
    public bool IsEmpty => RawBytes.Length == 0;

    /// <summary>
    /// Creates a <see cref="PdfText"/> from a PDF string operand.
    /// </summary>
    public static PdfText FromOperand(IPdfValue operand)
    {
        PdfString? value = operand.AsString();

        if (value?.IsEmpty != false)
        {
            return default;
        }

        return new PdfText(value.Value.Value);
    }

    /// <inheritdoc/>
    public override string ToString() => EncodingExtensions.PdfDefault.GetString(RawBytes);
}
