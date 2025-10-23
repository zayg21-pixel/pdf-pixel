using System;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Huffman;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg
{
    /// <summary>
    /// Fast Huffman decoder built from a <see cref="JpgHuffmanTable"/>. Uses a widened lookahead table (10 bits by default)
    /// to capture the majority of codes in a single table probe. Fallback performs a single 16-bit peek and
    /// incrementally extends the code length without discarding whole bytes, ensuring only the exact number of
    /// bits for the matched code are removed from the bit reader.
    /// </summary>
    internal sealed class JpgHuffmanDecoder
    {
        private const int LookaheadBits = 8; // MUST be &lt;= MaxCodeBits and &lt;= 16 (since we only peek 16 bits).
        private const int MaxCodeBits = 16;

        // Lookahead table: entry layout: high byte = number of bits in code, low byte = symbol.
        // Entry value -1 indicates no direct match (requires slow path).
        private readonly short[] _lookahead;

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

                    for (int valueIndex = 0; valueIndex < count; valueIndex++)
                    {
                        if (codeLength <= LookaheadBits)
                        {
                            int replicateCount = 1 << (LookaheadBits - codeLength);
                            int baseIndex = code << (LookaheadBits - codeLength);
                            short entry = (short)((codeLength << 8) | table.Values[huffValueIndex + valueIndex]);
                            for (int replicateIndex = 0; replicateIndex < replicateCount; replicateIndex++)
                            {
                                _lookahead[baseIndex + replicateIndex] = entry;
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
        /// Decode a single Huffman symbol from the bit reader. Performs a single 16-bit peek supplying the
        /// high bits used for the widened lookahead table. On fast-path a single array access plus DropBits.
        /// Slow path incrementally extends the code without re-shifting the entire 16-bit window for each length.
        /// </summary>
        /// <param name="br">Bit reader positioned at the next Huffman code.</param>
        /// <returns>Decoded symbol or -1 on malformed input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(ref JpgBitReader br)
        {
            uint peek16 = br.PeekBits16();
            int lookaheadShift = MaxCodeBits - LookaheadBits;
            int lookaheadIndex = (int)(peek16 >> lookaheadShift);

            short lookaheadEntry = _lookahead[lookaheadIndex];
            if (lookaheadEntry != -1)
            {
                int matchedBits = (lookaheadEntry >> 8) & 0xFF;
                int symbol = lookaheadEntry & 0xFF;
                br.DropBits(matchedBits);
                return symbol;
            }

            int codePrefix = lookaheadIndex; // Current code bits value (length = LookaheadBits initially).
            for (int length = LookaheadBits + 1; length <= MaxCodeBits; length++)
            {
                int nextBit = (int)(peek16 >> (MaxCodeBits - length)) & 1;
                codePrefix = (codePrefix << 1) | nextBit;

                ushort max = _maxCode[length];
                if (codePrefix > max)
                {
                    continue;
                }

                ushort min = _minCode[length];
                if (codePrefix < min)
                {
                    continue;
                }

                int valueArrayIndex = _valOffset[length] + codePrefix;
                if ((uint)valueArrayIndex >= (uint)_huffval.Length)
                {
                    return -1;
                }

                br.DropBits(length);
                return _huffval[valueArrayIndex];
            }

            return -1; // Malformed stream (no matching code up to 16 bits).
        }
    }
}
