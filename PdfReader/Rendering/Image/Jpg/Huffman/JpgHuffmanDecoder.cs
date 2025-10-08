using System;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Huffman;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg
{
    /// <summary>
    /// Fast Huffman decoder built from a JpgHuffmanTable. Uses a lookahead table for up to 8-bit codes,
    /// falling back to a length-by-length scan using a single 16-bit peek beyond the lookahead size.
    /// </summary>
    internal sealed class JpgHuffmanDecoder
    {
        private const int LookaheadBits = 8;
        private const int MaxCodeBits = 16;
        private readonly short[] _lookahead; // 256 entries: high byte = bits, low byte = symbol (0 means invalid when entry == -1)
        private readonly ushort[] _minCode = new ushort[MaxCodeBits + 1];
        private readonly ushort[] _maxCode = new ushort[MaxCodeBits + 1];
        private readonly int[] _valOffset = new int[MaxCodeBits + 1];
        private readonly byte[] _huffval;

        public JpgHuffmanDecoder(JpgHuffmanTable table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            _lookahead = new short[1 << LookaheadBits];
            _huffval = table.Values;

            for (int i = 0; i < _lookahead.Length; i++)
            {
                _lookahead[i] = -1;
            }

            int huffValueIndex = 0;
            int code = 0;
            for (int codeLength = 1; codeLength <= MaxCodeBits; codeLength++)
            {
                int count = table.CodeLengthCounts[codeLength - 1];
                if (count == 0)
                {
                    _minCode[codeLength] = 0xFFFF;
                    _maxCode[codeLength] = 0xFFFF;
                    _valOffset[codeLength] = huffValueIndex - code;
                }
                else
                {
                    _minCode[codeLength] = (ushort)code;
                    _maxCode[codeLength] = (ushort)(code + count - 1);
                    _valOffset[codeLength] = huffValueIndex - code;

                    for (int j = 0; j < count; j++)
                    {
                        if (codeLength <= LookaheadBits)
                        {
                            int replicateCount = 1 << (LookaheadBits - codeLength);
                            int baseIndex = code << (LookaheadBits - codeLength);
                            short entry = (short)((codeLength << 8) | table.Values[huffValueIndex + j]);
                            for (int r = 0; r < replicateCount; r++)
                            {
                                _lookahead[baseIndex + r] = entry;
                            }
                        }

                        code++;
                    }
                }

                code <<= 1;
                huffValueIndex += count;
            }
        }

        /// <summary>
        /// Decode a single Huffman symbol from the bit reader.
        /// </summary>
        /// <param name="br">Bit reader positioned at the next Huffman code.</param>
        /// <returns>Decoded symbol or -1 on malformed input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(ref JpgBitReader br)
        {
            short laEntry = _lookahead[br.PeekBits8()];
            if (laEntry != -1)
            {
                int nbits = (laEntry >> 8) & 0xFF;
                int symbol = laEntry & 0xFF;
                br.DropBits(nbits);
                return symbol;
            }

            // Slow path: use one 16-bit peek, then test increasing lengths.
            // This avoids per-bit reads; we compute the candidate code by masking the low bits.
            uint bits16 = br.PeekBits16();
            for (int len = LookaheadBits + 1; len <= MaxCodeBits; len++)
            {
                int code = (int)(bits16 >> (MaxCodeBits - len)) & ((1 << len) - 1);
                ushort max = _maxCode[len];
                if (code > max)
                {
                    continue;
                }

                ushort min = _minCode[len];
                if (code < min)
                {
                    continue;
                }

                int index = _valOffset[len] + code;
                // Construction guarantees index is valid if tables were well-formed.
                if ((uint)index >= (uint)_huffval.Length)
                {
                    return -1;
                }

                br.DropBits(len);
                return _huffval[index];
            }

            return -1;
        }
    }
}
