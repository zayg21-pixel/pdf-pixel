using System;

namespace PdfReader.Fonts.Cff
{
    internal ref struct CffDataReader
    {
        private readonly ReadOnlySpan<byte> _data;
        public int Position;

        public CffDataReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            Position = 0;
        }

        public bool TryReadByte(out byte value)
        {
            if (Position >= _data.Length) { value = 0; return false; }
            value = _data[Position++];
            return true;
        }

        public bool TryReadUInt16BE(out ushort value)
        {
            if (Position + 1 >= _data.Length) { value = 0; return false; }
            value = (ushort)(_data[Position] << 8 | _data[Position + 1]);
            Position += 2;
            return true;
        }

        public bool TryReadOffset(int offSize, out int value)
        {
            value = 0;
            if (offSize < 1 || offSize > 4) return false;
            if (Position + offSize > _data.Length) return false;
            for (int i = 0; i < offSize; i++)
            {
                value = value << 8 | _data[Position + i];
            }
            Position += offSize;
            return true;
        }
    }
}
