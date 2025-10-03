using System;
using PdfReader.Rendering.Image.Jpg.Huffman;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg
{
    /// <summary>
    /// Fast Huffman decoder built from a JpgHuffmanTable. Uses a lookahead table for up to 8-bit codes,
    /// falling back to bit-by-bit decoding beyond the lookahead size.
    /// </summary>
    internal sealed class JpgHuffmanDecoder
    {
        private const int LookaheadBits = 8;
        private readonly short[] _lookahead; // 256 entries: high byte = bits, low byte = symbol (0 means invalid)
        private readonly ushort[] _minCode = new ushort[17];
        private readonly ushort[] _maxCode = new ushort[17];
        private readonly int[] _valOffset = new int[17];
        private readonly byte[] _huffval;

        public JpgHuffmanDecoder(JpgHuffmanTable table)
        {
            _lookahead = new short[1 << LookaheadBits];
            _huffval = table.Values;

            for (int i = 0; i < _lookahead.Length; i++)
            {
                _lookahead[i] = -1; // use -1 for invalid to allow symbol 0
            }

            int k = 0;    // index into huffval
            int code = 0; // running code value for current length
            for (int i = 1; i <= 16; i++)
            {
                int count = table.CodeLengthCounts[i - 1];
                if (count == 0)
                {
                    _minCode[i] = 0xFFFF;
                    _maxCode[i] = 0xFFFF;
                    _valOffset[i] = k - code;
                }
                else
                {
                    _minCode[i] = (ushort)code;
                    _maxCode[i] = (ushort)(code + count - 1);
                    _valOffset[i] = k - code;

                    for (int j = 0; j < count; j++)
                    {
                        if (i <= LookaheadBits)
                        {
                            int start = (code << (LookaheadBits - i)) & ((1 << LookaheadBits) - 1);
                            int end = (((code + 1) << (LookaheadBits - i)) - 1) & ((1 << LookaheadBits) - 1);
                            for (int idx = start; idx <= end; idx++)
                            {
                                _lookahead[idx] = (short)((i << 8) | table.Values[k + j]);
                            }
                        }

                        code++;
                    }
                }

                code <<= 1; // next length doubles code space
                k += count;
            }
        }

        public int Decode(ref JpgBitReader br)
        {
            br.EnsureBits(LookaheadBits);
            int la = (int)br.PeekBits(LookaheadBits);
            short entry = _lookahead[la];
            if (entry != -1)
            {
                int nbits = (entry >> 8) & 0xFF;
                int sym = entry & 0xFF;
                br.DropBits(nbits);
                return sym;
            }

            int code = 0;
            for (int len = 1; len <= 16; len++)
            {
                code = (code << 1) | (int)br.ReadBits(1);

                if (_maxCode[len] == 0xFFFF)
                {
                    continue;
                }

                if (code < _minCode[len])
                {
                    continue;
                }

                if (code <= _maxCode[len])
                {
                    int idx = _valOffset[len] + code;
                    if ((uint)idx < (uint)_huffval.Length)
                    {
                        return _huffval[idx];
                    }

                    break;
                }
            }

            return -1;
        }
    }
}
