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

        /// <summary>
        /// Returns true if the requested byte range is fully inside the underlying buffer.
        /// </summary>
        public bool CanRead(int offset, int count)
        {
            if (offset < 0 || count < 0)
            {
                return false;
            }
            if (offset > _data.Length)
            {
                return false;
            }
            return offset + count <= _data.Length;
        }

        /// <summary>
        /// Internal bounds check that throws on failure. Use <see cref="CanRead"/> for non-throwing probes.
        /// </summary>
        private void Ensure(int offset, int count)
        {
            if (!CanRead(offset, count))
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
            long hi = ReadInt32(offset);
            uint lo = (uint)ReadInt32(offset + 4);
            return (hi << 32) | lo;
        }

        public byte[] ReadBytes(int offset, int count)
        {
            Ensure(offset, count);
            var buffer = new byte[count];
            Buffer.BlockCopy(_data, offset, buffer, 0, count);
            return buffer;
        }

        public string ReadAscii(int offset, int count)
        {
            Ensure(offset, count);
            return Encoding.ASCII.GetString(_data, offset, count);
        }

        // ICC fixed-point helpers
        public static float S15Fixed16ToSingle(int value)
        {
            return value / 65536f; // 1 << 16
        }

        public static float U16Fixed16ToSingle(uint value)
        {
            return value / 65536f;
        }

        public static float U8Fixed8ToSingle(ushort value)
        {
            return value / 256f; // 1 << 8
        }

        public static uint FourCC(string fourCC)
        {
            if (fourCC == null || fourCC.Length != 4)
            {
                throw new ArgumentException("FourCC must be exactly 4 characters.", nameof(fourCC));
            }
            return (uint)(fourCC[0] << 24 | fourCC[1] << 16 | fourCC[2] << 8 | fourCC[3]);
        }

        public static string FourCCToString(uint sig)
        {
            char c0 = (char)(sig >> 24 & 0xFF);
            char c1 = (char)(sig >> 16 & 0xFF);
            char c2 = (char)(sig >> 8 & 0xFF);
            char c3 = (char)(sig & 0xFF);
            return new string(new[] { c0, c1, c2, c3 });
        }
    }
}
