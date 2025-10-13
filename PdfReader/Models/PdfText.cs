using System;
using System.Runtime.CompilerServices;
using System.Text;
using PdfReader.Fonts;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
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
        /// Convert CIDs to GIDs for font rendering using the existing font mapping path.
        /// Prefer Composite font code->CID mapping when available.
        /// </summary>
        public ushort[] GetGids(PdfCharacterCode[] codes, PdfFontBase font)
        {
            return ConvertCodesToGids(codes, font);
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort[] ConvertCodesToGids(PdfCharacterCode[] codes, PdfFontBase font)
        {
            if (codes?.Length == 0)
            {
                return [];
            }

            return font switch
            {
                PdfCompositeFont compositeFont => ConvertCodesToGidsCompositeFont(codes, compositeFont),
                PdfCIDFont cidFont => ConvertCidsToGidsCidFont(codes, cidFont),
                PdfSimpleFont simpleFont => ConvertCodesToGidsSimpleFont(codes, simpleFont),
                _ => IdentityCodesToGidsFallback(codes),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort[] ConvertCodesToGidsSimpleFont(PdfCharacterCode[] codes, PdfSimpleFont simpleFont)
        {
            var result = new ushort[codes.Length];
            var cff = simpleFont.FontDescriptor?.GetCffInfo();

            if (cff == null)
            {
                return IdentityCodesToGidsFallback(codes);
            }

            var differences = simpleFont.Differences;

            for (int i = 0; i < codes.Length; i++)
            {
                uint cid = codes[i];
                ushort gid = 0;

                string name = null;
                bool hasDifference = differences != null && differences.TryGetValue((int)cid, out name) && !string.IsNullOrEmpty(name);

                if (hasDifference && cff.NameToGid.TryGetValue(name, out ushort namedGid))
                {
                    gid = namedGid;
                }

                if (!hasDifference &&
                    SingleByteEncodingConverter.TryGetNameByCid(cid, simpleFont.Encoding, out string nameByCid) &&
                    cff.NameToGid.TryGetValue(nameByCid, out var standardNamedGid))
                {
                    gid = standardNamedGid;
                }

                result[i] = gid;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort[] ConvertCodesToGidsCompositeFont(PdfCharacterCode[] codes, PdfCompositeFont compositeFont)
        {
            var primary = compositeFont.PrimaryDescendant;
            if (primary == null)
            {
                return IdentityCodesToGidsFallback(codes);
            }

            var gids = new ushort[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                uint cid = 0;
                bool mapped = compositeFont.TryMapCodeToCid(codes[i], out cid);
                if (!mapped)
                {
                    // As a fallback, use big-endian packing (works for Identity encodings)
                    cid = (uint)codes[i];
                }

                gids[i] = primary.GetGlyphId(cid);
            }

            return gids;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort[] ConvertCidsToGidsCidFont(PdfCharacterCode[] codes, PdfCIDFont cidFont)
        {
            var gids = new ushort[codes.Length];

            for (int i = 0; i < codes.Length; i++)
            {
                gids[i] = cidFont.GetGlyphId(codes[i]);
            }

            return gids;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort[] IdentityCodesToGidsFallback(PdfCharacterCode[] codes)
        {
            ushort[] result = new ushort[codes.Length];

            for (int i = 0; i < codes.Length; i++)
            {
                result[i] = (ushort)(codes[i] & 0xFFFF);
            }

            return result;
        }

        public override string ToString()
        {
            return EncodingExtensions.PdfDefault.GetString(RawBytes);
        }
    }
}