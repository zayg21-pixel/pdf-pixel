using System;
using System.Text;

namespace PdfReader.Icc
{
    /// <summary>
    /// Minimal big-endian reader for ICC parsing. ICC uses big-endian for multi-byte numbers.
    /// Compatible with .NET Standard 2.0 (no spans).
    /// </summary>
    internal sealed class BigEndianReader
    {
        private readonly byte[] _data;

        public BigEndianReader(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
        }

        public int Length => _data.Length;

        public void Ensure(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > _data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Insufficient data to read the requested structure.");
            }
        }

        public byte ReadByte(int offset)
        {
            Ensure(offset, 1);
            return _data[offset];
        }

        public ushort ReadUInt16(int offset)
        {
            Ensure(offset, 2);
            return (ushort)((_data[offset] << 8) | _data[offset + 1]);
        }

        public short ReadInt16(int offset)
        {
            Ensure(offset, 2);
            return unchecked((short)((_data[offset] << 8) | _data[offset + 1]));
        }

        public uint ReadUInt32(int offset)
        {
            Ensure(offset, 4);
            return (uint)((_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3]);
        }

        public int ReadInt32(int offset)
        {
            Ensure(offset, 4);
            return unchecked((int)((_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3]));
        }

        public ulong ReadUInt64(int offset)
        {
            Ensure(offset, 8);
            var hi = ReadUInt32(offset);
            var lo = ReadUInt32(offset + 4);
            return ((ulong)hi << 32) | lo;
        }

        public long ReadInt64(int offset)
        {
            Ensure(offset, 8);
            var hi = (long)ReadInt32(offset);
            var lo = (uint)ReadInt32(offset + 4);
            return (hi << 32) | lo;
        }

        public byte[] ReadBytes(int offset, int count)
        {
            Ensure(offset, count);
            var buf = new byte[count];
            Buffer.BlockCopy(_data, offset, buf, 0, count);
            return buf;
        }

        public string ReadAscii(int offset, int count)
        {
            Ensure(offset, count);
            return Encoding.ASCII.GetString(_data, offset, count);
        }

        public static uint FourCC(string fourCC)
        {
            // Accept exactly 4 characters per ICC spec.
            if (string.IsNullOrEmpty(fourCC) || fourCC.Length != 4)
                throw new ArgumentException("fourCC must be exactly 4 ASCII chars", nameof(fourCC));
            return (uint)(
                (byte)fourCC[0] << 24 |
                (byte)fourCC[1] << 16 |
                (byte)fourCC[2] << 8 |
                (byte)fourCC[3]);
        }

        public static string FourCCToString(uint sig)
        {
            var a = (char)((sig >> 24) & 0xFF);
            var b = (char)((sig >> 16) & 0xFF);
            var c = (char)((sig >> 8) & 0xFF);
            var d = (char)(sig & 0xFF);
            return new string(new[] { a, b, c, d });
        }

        // ICC fixed-point helpers
        public static float S15Fixed16ToSingle(int value)
        {
            return value / 65536f;
        }

        public static float U16Fixed16ToSingle(uint value)
        {
            return value / 65536f;
        }

        public static float U8Fixed8ToSingle(ushort value)
        {
            return value / 256f;
        }
    }
}
