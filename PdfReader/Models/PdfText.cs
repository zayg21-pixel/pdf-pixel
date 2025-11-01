using System;
using PdfReader.Text;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents text extracted from PDF with proper handling of CID fonts vs Unicode fonts
    /// Refactored to use PdfFontBase hierarchy with enhanced font type support.
    /// Preserves raw bytes as ReadOnlyMemory<byte> to avoid extra allocations.
    /// </summary>
    public readonly struct PdfText
    {
        public PdfText(ReadOnlyMemory<byte> rawBytes)
        {
            RawBytes = rawBytes;
        }

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
            var bytes = operand.AsStringBytes();

            if (bytes.IsEmpty)
            {
                return default;
            }

            return new PdfText(bytes);
        }

        public override string ToString()
        {
            return EncodingExtensions.PdfDefault.GetString(RawBytes);
        }
    }
}