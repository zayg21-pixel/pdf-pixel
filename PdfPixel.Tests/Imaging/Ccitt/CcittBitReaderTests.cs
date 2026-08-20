using System;
using PdfPixel.Ccitt;
using Xunit;

namespace PdfPixel.Tests.Imaging.Ccitt;

/// <summary>
/// Covers how the fax bit reader recognises the end-of-line marker. ITU-T T.4 lets an encoder
/// pad the marker with a run of fill zeros, so the marker is not always the first thing at the
/// reader's position.
/// </summary>
public class CcittBitReaderTests
{
    /// <summary>
    /// A marker sitting exactly at the reader's position is consumed, leaving the bits after it.
    /// </summary>
    [Fact]
    public void TryConsumeEol_MarkerAtPosition_ConsumesIt()
    {
        // 000000000001 then a single set bit.
        byte[] data = [0x00, 0x18];
        ReadOnlySpan<byte> span = data;
        CcittBitReader reader = new(span, 0, 0, 0);

        Assert.True(reader.TryConsumeEol());
        Assert.Equal(1, reader.ReadBit());
    }

    /// <summary>
    /// A marker padded with fill zeros is still an end-of-line marker and has to be consumed
    /// along with its padding.
    /// </summary>
    [Fact]
    public void TryConsumeEol_MarkerAfterFillBits_ConsumesFillAndMarker()
    {
        // Four fill zeros, then 000000000001, then a single set bit.
        byte[] data = [0x00, 0x01, 0x80];
        ReadOnlySpan<byte> span = data;
        CcittBitReader reader = new(span, 0, 0, 0);

        Assert.True(reader.TryConsumeEol());
        Assert.Equal(1, reader.ReadBit());
    }

    /// <summary>
    /// Data that is not a marker leaves the reader where it was, so the caller can decode it.
    /// </summary>
    [Fact]
    public void TryConsumeEol_NoMarker_LeavesPositionUnchanged()
    {
        byte[] data = [0xB4, 0x2C];
        ReadOnlySpan<byte> span = data;
        CcittBitReader reader = new(span, 0, 0, 0);

        Assert.False(reader.TryConsumeEol());
        Assert.Equal(1, reader.ReadBit());
        Assert.Equal(0, reader.ReadBit());
        Assert.Equal(1, reader.ReadBit());
        Assert.Equal(1, reader.ReadBit());
    }

    /// <summary>
    /// A run of zeros that never reaches a marker is not an end-of-line marker, and the reader
    /// has to be left where it was rather than stranded in the middle of the run.
    /// </summary>
    [Fact]
    public void TryConsumeEol_ZerosWithoutMarker_LeavesPositionUnchanged()
    {
        byte[] data = [0x00, 0x00, 0x00];
        ReadOnlySpan<byte> span = data;
        CcittBitReader reader = new(span, 0, 0, 0);

        Assert.False(reader.TryConsumeEol());
        Assert.Equal(0, reader.ReadBit());
    }
}
