using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Fonts.Mapping
{
    internal class ExtractHelpers
    {
        /// <summary>
        /// Converts a 4-character tag to UInt32 as used in font tables.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ConvertTagToUInt32(string tag)
        {
            if (tag == null || tag.Length != 4)
            {
                throw new ArgumentException("Tag must be exactly 4 characters.", nameof(tag));
            }
            return (uint)tag[0] << 24 | (uint)tag[1] << 16 | (uint)tag[2] << 8 | tag[3];
        }

        /// <summary>
        /// Reads a UInt16 from a byte array at the specified offset (big-endian).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] << 8 | data[offset + 1]);
        }

        /// <summary>
        /// Reads a UInt32 from a byte array at the specified offset (big-endian).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)data[offset] << 24 |
                   (uint)data[offset + 1] << 16 |
                   (uint)data[offset + 2] << 8 |
                   data[offset + 3];
        }
    }
}