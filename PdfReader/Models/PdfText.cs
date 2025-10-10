using System;
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
        public uint[] GetGids(PdfCharacterCode[] codes, PdfFontBase font)
        {
            if (codes == null || codes.Length == 0)
            {
                return Array.Empty<uint>();
            }

            if (font is PdfCompositeFont composite)
            {
                var primary = composite.PrimaryDescendant;
                if (primary == null)
                {
                    return Array.Empty<uint>();
                }

                var gids = new uint[codes.Length];
                for (int i = 0; i < codes.Length; i++)
                {
                    uint cid = 0;
                    bool mapped = composite.TryMapCodeToCid(codes[i], out cid);
                    if (!mapped)
                    {
                        // As a fallback, use big-endian packing (works for Identity encodings)
                        cid = (uint)codes[i];
                    }

                    gids[i] = primary.GetGlyphId(cid);
                }

                return gids;
            }

            // Existing path for non-composite fonts
            var numericCids = new uint[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                numericCids[i] = (uint)codes[i];
            }

            return ConvertCIDsToGIDs(numericCids, font);
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

        private static ReadOnlyMemory<byte> GetStringBytes(IPdfValue operand)
        {
            var stringValue = operand.AsString();
            if (string.IsNullOrEmpty(stringValue))
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return EncodingExtensions.PdfDefault.GetBytes(stringValue);
        }

        private static ReadOnlyMemory<byte> GetHexStringBytes(IPdfValue operand)
        {
            var result = operand.AsHexBytes();
            if (result == null)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return result;
        }

        private static uint[] ConvertCIDsToGIDs(uint[] cids, PdfFontBase font)
        {
            if (cids?.Length == 0)
            {
                return Array.Empty<uint>();
            }
            
            switch (font)
            {
                case PdfCompositeFont compositeFont:
                    return ConvertCIDsToGIDs(cids, compositeFont);

                case PdfCIDFont cidFont:
                    return ConvertCIDsToGIDs(cids, cidFont);

                case PdfSimpleFont simpleFont:
                    return ConvertCIDsToGIDsSimple(cids, simpleFont);

                case PdfType3Font type3Font:
                    return ConvertCIDsToGIDs(cids, type3Font);

                default:
                    return cids;
            }
        }

        private static uint[] ConvertCIDsToGIDsSimple(uint[] cids, PdfSimpleFont simpleFont)
        {
            var result = new uint[cids.Length];
            var cff = simpleFont.FontDescriptor?.GetCffInfo();

            if (cff == null)
            {
                for (int i = 0; i < cids.Length; i++)
                {
                    result[i] = cids[i] & 0xFF;
                }

                return result;
            }

            var differences = simpleFont.Differences;

            for (int i = 0; i < cids.Length; i++)
            {
                uint cid = cids[i];
                uint gid = 0;

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

        private static uint[] ConvertCIDsToGIDs(uint[] cids, PdfCompositeFont compositeFont)
        {
            var map = compositeFont.PrimaryDescendant?.CIDToGIDMap;

            if (map != null)
            {
                uint[] gids = new uint[cids.Length];

                for (int i = 0; i < cids.Length; i++)
                {
                    var cid = cids[i];
                    gids[i] = map.GetGID(cid);
                }

                return gids;
            }

            return cids;
        }

        private static uint[] ConvertCIDsToGIDs(uint[] cids, PdfCIDFont cidFont)
        {
            var gids = new uint[cids.Length];
            for (int i = 0; i < cids.Length; i++)
            {
                var cid = cids[i];
                gids[i] = cidFont.GetGlyphId(cid);
            }
            return gids;
        }

        private static uint[] ConvertCIDsToGIDs(uint[] cids, PdfType3Font type3Font)
        {
            return cids;
        }

        public override string ToString()
        {
            return EncodingExtensions.PdfDefault.GetString(RawBytes);
        }
    }
}