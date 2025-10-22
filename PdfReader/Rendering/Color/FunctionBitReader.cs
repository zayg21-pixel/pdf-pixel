using System;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Reads arbitrary bit-width values from a byte array, used for PDF sampled functions.
    /// </summary>
    internal sealed class FunctionBitReader
    {
        private readonly byte[] _data;
        private int _bitPosition;

        public FunctionBitReader(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
            _bitPosition = 0;
        }

        /// <summary>
        /// Reads the specified number of bits and returns the value as an unsigned integer.
        /// </summary>
        /// <param name="count">Number of bits to read (1-32).</param>
        /// <returns>Unsigned integer value of the read bits.</returns>
        public uint ReadBits(int count)
        {
            if (count <= 0 || count > 32)
            {
                return 0;
            }

            uint value = 0;
            for (int bitIndex = 0; bitIndex < count; bitIndex++)
            {
                int byteIndex = _bitPosition >> 3;
                if (byteIndex >= _data.Length)
                {
                    break;
                }

                int shift = 7 - (_bitPosition & 7);
                uint bit = (uint)((_data[byteIndex] >> shift) & 1);
                value = (value << 1) | bit;
                _bitPosition++;
            }
            return value;
        }
    }
}
