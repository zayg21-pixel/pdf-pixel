using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace PdfPixel.Png.Encoding;

/// <summary>
/// Compresses data into a zlib stream (RFC 1950): header, deflate data and Adler-32 trailer.
/// </summary>
internal sealed class PngZlibEncoder : IDisposable
{
    private const uint AdlerModulo = 65521;
    private const byte CompressionMethodAndWindow = 0x78;

    private readonly MemoryStream _output = new();
    private readonly DeflateStream _deflateStream;

    private uint _adlerLow = 1;
    private uint _adlerHigh;

    public PngZlibEncoder(CompressionLevel compressionLevel)
    {
        _output.WriteByte(CompressionMethodAndWindow);
        _output.WriteByte(GetFlags(compressionLevel));

        _deflateStream = new DeflateStream(_output, compressionLevel, leaveOpen: true);
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        _deflateStream.Write(buffer, offset, count);

        for (int index = offset; index < offset + count; index++)
        {
            _adlerLow = (_adlerLow + buffer[index]) % AdlerModulo;
            _adlerHigh = (_adlerHigh + _adlerLow) % AdlerModulo;
        }
    }

    /// <summary>
    /// Ends the deflate data, appends the Adler-32 trailer and returns the whole zlib stream.
    /// </summary>
    public byte[] Finish()
    {
        _deflateStream.Dispose();

        var adler = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, (_adlerHigh << 16) | _adlerLow);
        _output.Write(adler, 0, adler.Length);

        return _output.ToArray();
    }

    public static byte[] Compress(byte[] data, CompressionLevel compressionLevel)
    {
        using PngZlibEncoder encoder = new(compressionLevel);
        encoder.Write(data, 0, data.Length);

        return encoder.Finish();
    }

    /// <summary>
    /// Returns the FLG byte: the FLEVEL of the compression level and the FCHECK bits (RFC 1950 section 2.2).
    /// </summary>
    private static byte GetFlags(CompressionLevel compressionLevel)
    {
        int level = compressionLevel switch
        {
            CompressionLevel.NoCompression => 0,
            CompressionLevel.Fastest => 1,
            CompressionLevel.Optimal => 2,
            _ => 3
        };

        int flags = level << 6;
        int remainder = ((CompressionMethodAndWindow << 8) | flags) % 31;

        return (byte)((remainder == 0) ? flags : flags + 31 - remainder);
    }

    public void Dispose()
    {
        _deflateStream.Dispose();
        _output.Dispose();
    }
}
