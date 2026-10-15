using System;
using System.IO;
using System.IO.Compression;

namespace PdfPixel.Png.Streams;

/// <summary>
/// Opens zlib streams (RFC 1950) of PNG image data and compressed chunks.
/// </summary>
internal static class PngZlibDecoder
{
    private const int DeflateMethod = 8;
    private const int PresetDictionaryFlag = 0x20;

    /// <summary>
    /// Reads and checks the zlib header, then returns a stream inflating the deflate data that follows.
    /// </summary>
    public static DeflateStream Open(Stream source)
    {
        int compressionMethod = source.ReadByte();
        int flags = source.ReadByte();

        if (compressionMethod < 0 || flags < 0)
        {
            throw new InvalidDataException("PNG zlib stream ends inside its header.");
        }

        if ((compressionMethod & 0x0F) != DeflateMethod || ((compressionMethod << 8) | flags) % 31 != 0)
        {
            throw new InvalidDataException("PNG zlib stream has an invalid header.");
        }

        if ((flags & PresetDictionaryFlag) != 0)
        {
            throw new InvalidDataException("PNG zlib stream declares a preset dictionary.");
        }

        return new DeflateStream(source, CompressionMode.Decompress, leaveOpen: false);
    }

    /// <summary>
    /// Inflates a whole zlib stream.
    /// </summary>
    public static byte[] Decompress(in ReadOnlySpan<byte> data)
    {
        using MemoryStream source = new(data.ToArray(), writable: false);
        using DeflateStream inflater = Open(source);
        using MemoryStream output = new();

        inflater.CopyTo(output);

        return output.ToArray();
    }
}
