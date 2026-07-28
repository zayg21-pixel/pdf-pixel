using PdfPixel.Jpx.Parsing;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Covers the bit-stuffing rules of ITU-T T.800 B.10.1. Packet headers are written so that
/// no 0xFF byte is ever followed by a byte whose most significant bit is set; the encoder
/// inserts a stuffing bit to guarantee it. A reader that ignores those inserted bits ends
/// the header at the wrong byte and desynchronises every packet that follows.
/// </summary>
public class JpxBitReaderTests
{
    /// <summary>
    /// A header ending on 0xFF is followed by a stuffing byte that carries no header data
    /// and is not part of the packet body, so aligning must consume it.
    /// </summary>
    [Fact]
    public void ByteAlign_AfterHeaderEndingOnFf_SkipsStuffingByte()
    {
        byte[] data = [0xCF, 0xCA, 0xFF, 0x00, 0x9D];
        JpxBitReader reader = new(data);

        reader.ReadBits(24);
        reader.ByteAlign();

        Assert.Equal(0x9D, reader.ReadRawSpan(1)[0]);
    }

    /// <summary>
    /// The stuffing byte follows the 0xFF even when the aligning happens part-way through
    /// the byte after it, because the run of stuffed bits starts at the 0xFF itself.
    /// </summary>
    [Fact]
    public void ByteAlign_PartWayThroughByteAfterFf_SkipsStuffingByte()
    {
        byte[] data = [0x0F, 0xFF, 0x40, 0xAB];
        JpxBitReader reader = new(data);

        reader.ReadBits(12);
        reader.ByteAlign();

        Assert.Equal(0xAB, reader.ReadRawSpan(1)[0]);
    }

    /// <summary>
    /// Without a preceding 0xFF there is no stuffing byte, so aligning only discards the
    /// unread bits of the current byte.
    /// </summary>
    [Fact]
    public void ByteAlign_WithoutPrecedingFf_KeepsNextByte()
    {
        byte[] data = [0x0F, 0x12, 0xAB];
        JpxBitReader reader = new(data);

        reader.ReadBits(4);
        reader.ByteAlign();

        Assert.Equal(0x12, reader.ReadRawSpan(1)[0]);
    }

    /// <summary>
    /// Mid-header the byte following a 0xFF contributes only its seven low bits; the most
    /// significant bit is the stuffing bit.
    /// </summary>
    [Fact]
    public void ReadBits_AfterFf_TreatsMostSignificantBitAsStuffing()
    {
        byte[] data = [0xFF, 0xA5];
        JpxBitReader reader = new(data);

        Assert.Equal(0xFFu, reader.ReadBits(8));
        Assert.Equal(0x25u, reader.ReadBits(7));
    }
}
