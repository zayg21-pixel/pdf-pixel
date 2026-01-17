using PdfRender.Color.Structures;
using SkiaSharp;
using System;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfRender.Imaging.Png
{
    internal static class PngHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePngSignature(SKDynamicMemoryWStream stream)
        {
            stream.Write8(0x89);
            stream.Write8((byte)'P');
            stream.Write8((byte)'N');
            stream.Write8((byte)'G');
            stream.Write8(0x0D);
            stream.Write8(0x0A);
            stream.Write8(0x1A);
            stream.Write8(0x0A);
        }

        /// <summary>
        /// Writes the zlib signature (0x78, 0x01) to the stream and updates the CRC.
        /// </summary>
        /// <param name="stream">The output stream.</param>
        /// <param name="crc32">The CRC32 instance to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteZLibSignature(SKDynamicMemoryWStream stream, Crc32 crc32)
        {
            const byte ZlibHeader1 = 0x78;
            const byte ZlibHeader2 = 0x01;
            byte[] zlibHeader = [ZlibHeader1, ZlibHeader2];
            stream.Write(zlibHeader, 2);
            crc32.Append(zlibHeader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteIccpChunk(SKDynamicMemoryWStream stream, ReadOnlyMemory<byte> iccProfile)
        {
            const string ProfileName = "ICC profile";
            byte[] nameBytes = Encoding.ASCII.GetBytes(ProfileName);
            using var chunkData = new MemoryStream();
            chunkData.Write(nameBytes, 0, nameBytes.Length);
            chunkData.WriteByte(0); // Null terminator
            chunkData.WriteByte(0); // Compression method: 0 = deflate

            // Write zlib header (0x78, 0x9C for default/optimal compression)
            chunkData.WriteByte(0x78);
            chunkData.WriteByte(0x9C);

            // Compress the ICC profile using DeflateStream (raw DEFLATE, no zlib header)
            using (var deflate = new DeflateStream(chunkData, CompressionLevel.Optimal, true))
            {
#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                deflate.Write(iccProfile.Span);
#else
                deflate.Write(iccProfile.ToArray(), 0, iccProfile.Length);
#endif
            }

            // Write the iCCP chunk using the buffer (including adler)
            WriteChunk(stream, "iCCP", chunkData.ToArray(), (int)chunkData.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePlteChunk(SKDynamicMemoryWStream stream, RgbaPacked[] palette)
        {
            int count = palette.Length;
            byte[] plte = new byte[count * 3];
            for (int i = 0; i < count; i++)
            {
                plte[i * 3 + 0] = palette[i].R;
                plte[i * 3 + 1] = palette[i].G;
                plte[i * 3 + 2] = palette[i].B;
            }
            WriteChunk(stream, "PLTE", plte, plte.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteIhdrChunk(SKDynamicMemoryWStream stream, int width, int height, byte bitDepth, byte colorType)
        {
            byte[] ihdr = new byte[13];
            WriteInt32BigEndian(ihdr, 0, width);
            WriteInt32BigEndian(ihdr, 4, height);
            ihdr[8] = bitDepth;
            ihdr[9] = colorType;
            ihdr[10] = 0; // Compression method (deflate)
            ihdr[11] = 0; // Filter method
            ihdr[12] = 0; // Interlace method (none)
            WriteChunk(stream, "IHDR", ihdr, ihdr.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChunkHeader(SKDynamicMemoryWStream stream, string type, int dataLength, Crc32 crc32)
        {
            if (type == null || type.Length != 4)
            {
                throw new ArgumentException("PNG chunk type must be 4 characters.", nameof(type));
            }
            WriteInt32BigEndian(stream, dataLength);
            byte[] typeBytes =
            [
                (byte)type[0],
                (byte)type[1],
                (byte)type[2],
                (byte)type[3]
            ];
            stream.Write(typeBytes, 4);
            crc32.Append(typeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChunkData(SKDynamicMemoryWStream stream, byte[] data, int length, Crc32 crc32)
        {
            if (length > 0)
            {
                stream.Write(data, length);
                crc32.Append(data.AsSpan().Slice(0, length));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CompleteChunk(SKDynamicMemoryWStream stream, Crc32 crc32)
        {
            var uint32Hash = crc32.GetCurrentHashAsUInt32();
            WriteUInt32BigEndian(stream, uint32Hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChunk(SKDynamicMemoryWStream stream, string type, byte[] data, int length)
        {
            Crc32 crc32 = new Crc32();
            WriteChunkHeader(stream, type, length, crc32);
            WriteChunkData(stream, data, length, crc32);
            CompleteChunk(stream, crc32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32BigEndian(SKDynamicMemoryWStream stream, int value)
        {
            stream.Write8((byte)(value >> 24 & 0xFF));
            stream.Write8((byte)(value >> 16 & 0xFF));
            stream.Write8((byte)(value >> 8 & 0xFF));
            stream.Write8((byte)(value & 0xFF));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24 & 0xFF);
            buffer[offset + 1] = (byte)(value >> 16 & 0xFF);
            buffer[offset + 2] = (byte)(value >> 8 & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32BigEndian(SKDynamicMemoryWStream stream, uint value)
        {
            stream.Write8((byte)(value >> 24 & 0xFF));
            stream.Write8((byte)(value >> 16 & 0xFF));
            stream.Write8((byte)(value >> 8 & 0xFF));
            stream.Write8((byte)(value & 0xFF));
        }

        /// <summary>
        /// Updates a 5-byte array with the DEFLATE uncompressed block header.
        /// </summary>
        /// <param name="block">The 5-byte array to update.</param>
        /// <param name="blockSize">The size of the block (max 65535).</param>
        /// <param name="isFinal">True if this is the final block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateDeflateBlockHeader(byte[] block, int blockSize, bool isFinal)
        {
            if (block == null || block.Length != 5)
            {
                throw new ArgumentException("Block header must be a 5-byte array.", nameof(block));
            }
            block[0] = isFinal ? (byte)1 : (byte)0;
            block[1] = (byte)(blockSize & 0xFF);
            block[2] = (byte)((blockSize >> 8) & 0xFF);
            ushort nlen = (ushort)~blockSize;
            block[3] = (byte)(nlen & 0xFF);
            block[4] = (byte)((nlen >> 8) & 0xFF);
        }
    }
}
