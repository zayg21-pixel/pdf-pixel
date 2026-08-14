using System;
using PdfPixel.Models;

namespace PdfPixel.Text
{
    /// <summary>
    /// Represents text extracted from PDF with proper handling of CID fonts vs Unicode fonts
    /// Refactored to use PdfFontBase hierarchy with enhanced font type support.
    /// Preserves raw bytes as <c>ReadOnlyMemory&lt;byte&gt;</c> to avoid extra allocations.
    /// </summary>
    public readonly struct PdfText
    {
        /// <summary>
        /// Initializes a new <see cref="PdfText"/> with the raw PDF character code bytes.
        /// </summary>
        public PdfText(in ReadOnlyMemory<byte> rawBytes) => RawBytes = rawBytes;

        /// <summary>
        /// Raw character codes/codepoints from the PDF (for HarfBuzz shaping of CID fonts)
        /// </summary>
        public ReadOnlyMemory<byte> RawBytes { get; }

        /// <summary>
        /// Check if the text is empty
        /// </summary>
        public bool IsEmpty => RawBytes.Length == 0;

        /// <summary>
        /// Create PdfText from a PDF string operand
        /// </summary>
        public static PdfText FromOperand(IPdfValue operand)
        {
            PdfString? value = operand.AsString();

            if (value == null || value.Value.IsEmpty)
            {
                return default;
            }

            return new PdfText(value.Value.Value);
        }

        /// <inheritdoc/>
        public override string ToString() => EncodingExtensions.PdfDefault.GetString(RawBytes);
    }
}
