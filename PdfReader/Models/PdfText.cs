using System;
using System.Runtime.CompilerServices;
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
            if (operand.Type == PdfValueType.String)
            {
                var rawBytes = GetStringBytes(operand);
                return new PdfText(rawBytes);
            }
            else if (operand.Type == PdfValueType.HexString)
            {
                var rawBytes = GetHexStringBytes(operand);
                return new PdfText(rawBytes);
            }
            else
            {
                return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlyMemory<byte> GetStringBytes(IPdfValue operand)
        {
            var stringValue = operand.AsString();
            if (string.IsNullOrEmpty(stringValue))
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return EncodingExtensions.PdfDefault.GetBytes(stringValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlyMemory<byte> GetHexStringBytes(IPdfValue operand)
        {
            var result = operand.AsHexBytes();
            if (result == null)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return result;
        }

        public override string ToString()
        {
            return EncodingExtensions.PdfDefault.GetString(RawBytes);
        }
    }
}