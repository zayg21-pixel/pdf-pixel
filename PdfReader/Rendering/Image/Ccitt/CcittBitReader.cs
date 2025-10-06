using System;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// Bit reader for CCITT fax streams (MSB-first within each byte).
    /// Provides minimal helpers for optional EOL handling and alignment.
    /// Exposes read state so callers can snapshot/restore between logical decode units (rows).
    /// </summary>
    internal ref struct CcittBitReader
    {
        private ReadOnlySpan<byte> _data;
        private int _byteIndex;
        private int _bitsRemaining; // number of bits remaining in _current
        private byte _current;      // shifted working byte
        private readonly bool _msbFirst;

        /// <summary>
        /// Create a new reader at the beginning of the data span.
        /// </summary>
        /// <param name="data">Encoded data span.</param>
        /// <param name="msbFirst">True for MSB-first (standard CCITT).</param>
        public CcittBitReader(ReadOnlySpan<byte> data, bool msbFirst = true)
        {
            _data = data;
            _byteIndex = 0;
            _bitsRemaining = 0;
            _current = 0;
            _msbFirst = msbFirst;
        }

        /// <summary>
        /// Create a reader with explicit state (useful for resuming mid-stream without keeping the ref struct alive).
        /// </summary>
        /// <param name="data">Encoded data span.</param>
        /// <param name="byteIndex">Current byte index within the data.</param>
        /// <param name="bitsRemaining">Bits remaining in the current working byte.</param>
        /// <param name="current">Current working byte (already shifted appropriately).</param>
        /// <param name="msbFirst">Bit order flag.</param>
        public CcittBitReader(ReadOnlySpan<byte> data, int byteIndex, int bitsRemaining, byte current, bool msbFirst = true)
        {
            _data = data;
            _byteIndex = byteIndex;
            _bitsRemaining = bitsRemaining;
            _current = current;
            _msbFirst = msbFirst;
        }

        /// <summary>
        /// Current byte index within the underlying data span.
        /// </summary>
        public int ByteIndex => _byteIndex;

        /// <summary>
        /// Remaining bit count in the working byte.
        /// </summary>
        public int BitsRemaining => _bitsRemaining;

        /// <summary>
        /// Current shifted working byte value.
        /// </summary>
        public byte Current => _current;

        public int ReadBit()
        {
            if (_bitsRemaining == 0)
            {
                if (_byteIndex >= _data.Length)
                {
                    return -1;
                }
                _current = _data[_byteIndex++];
                _bitsRemaining = 8;
            }

            int bit;
            if (_msbFirst)
            {
                bit = (_current >> 7) & 1;
                _current <<= 1;
            }
            else
            {
                bit = _current & 1;
                _current >>= 1;
            }
            _bitsRemaining--;
            return bit;
        }

        public void AlignAfterEndOfLine(bool byteAlign)
        {
            if (byteAlign)
            {
                _bitsRemaining = 0; // drop remainder
            }
        }

        public bool TryConsumeEol()
        {
            if (PeekBits(12) == 0x001)
            {
                AdvanceBits(12);
                return true;
            }
            return false;
        }

        public bool TryConsumeRtc()
        {
            // RTC = six consecutive EOLs (not frequently used in G4 but included for completeness)
            int saveByte = _byteIndex;
            int saveRemain = _bitsRemaining;
            byte saveCurrent = _current;
            int eolCount = 0;
            while (eolCount < 6)
            {
                if (!TryConsumeEol())
                {
                    _byteIndex = saveByte;
                    _bitsRemaining = saveRemain;
                    _current = saveCurrent;
                    return false;
                }
                eolCount++;
            }
            return true;
        }

        public int PeekBits(int count)
        {
            if (count <= 0 || count > 24)
            {
                return 0;
            }
            int saveByte = _byteIndex;
            int saveRemain = _bitsRemaining;
            byte saveCurrent = _current;
            int value = 0;
            for (int i = 0; i < count; i++)
            {
                int bit = ReadBit();
                if (bit < 0)
                {
                    value <<= (count - i); // pad with zeros
                    break;
                }
                value = (value << 1) | bit;
            }
            _byteIndex = saveByte;
            _bitsRemaining = saveRemain;
            _current = saveCurrent;
            return value;
        }

        private void AdvanceBits(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReadBit() < 0)
                {
                    break;
                }
            }
        }
    }
}
