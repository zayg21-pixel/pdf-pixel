using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Covers the byte alignment every packet header ends on. A header that leaves the reader
/// mid-byte makes the next packet start at the wrong bit, which desynchronises the rest of
/// the tile.
/// </summary>
public class JpxPacketHeaderParserTests
{
    /// <summary>
    /// An empty packet's header is a single zero bit, and the seven bits padding it to the
    /// byte boundary belong to that header rather than to the packet that follows.
    /// </summary>
    [Fact]
    public void ParsePacketHeader_EmptyPackets_ConsumeAWholeByteEach()
    {
        JpxHeader header = CreateSingleTileHeader();
        JpxTileHeader tileHeader = new()
        {
            TileIndex = 0,
            TilesHorizontal = 1,
            TilesVertical = 1
        };

        JpxPacketHeaderParser parser = new(header, tileHeader);

        byte[] packetData = [0x00, 0x00, 0xAB];
        JpxBitReader bitReader = new(packetData);

        Assert.Equal(0, parser.ParsePacketHeader(ref bitReader, 0, 0, 0, 0, 0).Length);
        Assert.Equal(0, parser.ParsePacketHeader(ref bitReader, 0, 0, 0, 0, 0).Length);

        Assert.Equal(2, bitReader.Position);
        Assert.Equal(0xAB, bitReader.ReadRawSpan(1)[0]);
    }

    private static JpxHeader CreateSingleTileHeader()
    {
        JpxComponent component = new()
        {
            SamplePrecision = 7,
            HorizontalSeparation = 1,
            VerticalSeparation = 1
        };

        JpxCodingStyle codingStyle = new()
        {
            NumberOfLayers = 1,
            DecompositionLevels = 0,
            CodeBlockWidthExponent = 4,
            CodeBlockHeightExponent = 4
        };

        JpxHeader header = new()
        {
            Width = 64,
            Height = 64,
            TileWidth = 64,
            TileHeight = 64,
            ComponentCount = 1,
            CodingStyle = codingStyle
        };

        header.Components.Add(component);

        return header;
    }
}
