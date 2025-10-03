using System;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// Bit reader for CCITT fax streams (MSB-first within each byte).
    /// Provides minimal helpers for optional EOL handling and alignment.
    /// </summary>
    internal ref struct CcittBitReader
    {
        private ReadOnlySpan<byte> _data;
        private int _byteIndex;
        private int _bitsRemaining; // number of bits remaining in _current
        private byte _current;      // shifted working byte
        private readonly bool _msbFirst;

        public CcittBitReader(ReadOnlySpan<byte> data, bool msbFirst = true)
        {
            _data = data;
            _byteIndex = 0;
            _bitsRemaining = 0;
            _current = 0;
            _msbFirst = msbFirst;
        }

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
