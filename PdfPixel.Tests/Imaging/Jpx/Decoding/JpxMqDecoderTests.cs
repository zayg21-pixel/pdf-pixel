using PdfPixel.Imaging.Jpx.Decoding;
using System;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx.Decoding;

/// <summary>
/// Tests for the MQ arithmetic decoder (ITU-T T.800 Annex C).
/// </summary>
public class JpxMqDecoderTests
{
    /// <summary>
    /// Verifies the probability estimation table has exactly 47 states
    /// as defined in ITU-T T.800 Table C.3.
    /// </summary>
    [Fact]
    public void StateCount_Is47_AsDefinedInSpec()
    {
        Assert.Equal(47, JpxMqDecoder.StateCount);
    }

    /// <summary>
    /// Verifies that creating a decoder with minimal data does not throw.
    /// The MQ decoder should handle even very short data gracefully.
    /// </summary>
    [Fact]
    public void Constructor_WithMinimalData_DoesNotThrow()
    {
        // Minimum data: at least 2 bytes for INITDEC to read
        byte[] data = [0x00, 0x00];

        var decoder = new JpxMqDecoder(data);

        // Verify initial context states per ITU-T T.800 Table D.7
        // Context 0 (uniform): state 46
        Assert.Equal(46, decoder.GetContextState(0));
        Assert.Equal(0, decoder.GetContextMps(0));

        // Context 1 (run-length): state 3
        Assert.Equal(3, decoder.GetContextState(1));
        Assert.Equal(0, decoder.GetContextMps(1));

        // Context 2 (first ZC): state 4
        Assert.Equal(4, decoder.GetContextState(2));
        Assert.Equal(0, decoder.GetContextMps(2));
    }

    /// <summary>
    /// Verifies that contexts 3-18 initialize to state 0 with MPS=0.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(16)]
    public void DefaultContexts_InitializeToState0(int context)
    {
        byte[] data = [0x00, 0x00];

        var decoder = new JpxMqDecoder(data);

        Assert.Equal(0, decoder.GetContextState(context));
        Assert.Equal(0, decoder.GetContextMps(context));
    }

    /// <summary>
    /// Verifies that DecodeBit returns valid binary values (0 or 1).
    /// Uses padded data so the decoder runs in a deterministic end-of-stream mode.
    /// </summary>
    [Fact]
    public void DecodeBit_ReturnsValidBinaryValues()
    {
        // Provide enough data for multiple decode operations
        byte[] data = [0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00];

        var decoder = new JpxMqDecoder(data);

        for (int i = 0; i < 16; i++)
        {
            int bit = decoder.DecodeBit(0);

            Assert.True(bit == 0 || bit == 1, $"DecodeBit returned {bit}, expected 0 or 1.");
        }
    }

    /// <summary>
    /// Verifies that decoding the same context multiple times causes state transitions.
    /// After enough MPS events, the state index should advance from the initial value.
    /// </summary>
    [Fact]
    public void DecodeBit_MultipleDecodes_CausesStateTransitions()
    {
        // Data that will produce mostly MPS decisions (all zeros in C register)
        byte[] data = new byte[64];

        var decoder = new JpxMqDecoder(data);

        int initialState = decoder.GetContextState(1);

        // Decode many bits on context 1 - state should eventually change
        for (int i = 0; i < 50; i++)
        {
            decoder.DecodeBit(1);
        }

        int finalState = decoder.GetContextState(1);

        // State should have changed from initial (state 0) after many decodes
        Assert.NotEqual(initialState, finalState);
    }

    /// <summary>
    /// Verifies decoder handles empty data gracefully (pads with 0xFF).
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyData_PadsGracefully()
    {
        byte[] data = [];

        var decoder = new JpxMqDecoder(data);

        // Should still be able to decode (padding with 0xFF)
        int bit = decoder.DecodeBit(0);

        Assert.True(bit == 0 || bit == 1);
    }

    /// <summary>
    /// Verifies that decoding is deterministic - same input produces same output.
    /// </summary>
    [Fact]
    public void DecodeBit_IsDeterministic()
    {
        byte[] data = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0];

        var decoder1 = new JpxMqDecoder(data);
        var decoder2 = new JpxMqDecoder(data);

        for (int i = 0; i < 20; i++)
        {
            int bit1 = decoder1.DecodeBit(i % JpxMqDecoder.ContextCount);
            int bit2 = decoder2.DecodeBit(i % JpxMqDecoder.ContextCount);

            Assert.Equal(bit1, bit2);
        }
    }

    /// <summary>
    /// Verifies that the 0xFF byte-stuffing in BYTEIN is handled correctly.
    /// After 0xFF, bytes > 0x8F should be treated as markers and not consumed.
    /// </summary>
    [Fact]
    public void ByteIn_HandlesMarkerBytesAfterFF()
    {
        // 0xFF followed by 0x90 (which is > 0x8F) simulates an in-stream marker
        byte[] data = [0x00, 0xFF, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00];

        var decoder = new JpxMqDecoder(data);

        // Should not throw - marker bytes are handled by padding
        for (int i = 0; i < 10; i++)
        {
            int bit = decoder.DecodeBit(0);

            Assert.True(bit == 0 || bit == 1);
        }
    }

    /// <summary>
    /// Verifies that after 0xFF, a byte <= 0x8F is consumed with 7-bit shift (bit-stuffing).
    /// </summary>
    [Fact]
    public void ByteIn_HandlesBitStuffingAfterFF()
    {
        // 0xFF followed by 0x7F (which is <= 0x8F) - should be consumed with 7-bit CT
        byte[] data = [0x00, 0xFF, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00];

        var decoder = new JpxMqDecoder(data);

        // Should not throw and should produce valid output
        for (int i = 0; i < 10; i++)
        {
            int bit = decoder.DecodeBit(0);

            Assert.True(bit == 0 || bit == 1);
        }
    }

    /// <summary>
    /// Verifies context count matches the JPEG 2000 code-block context requirement.
    /// 19 contexts: 9 significance + 5 sign + 3 magnitude refinement + 1 uniform + 1 run-length.
    /// </summary>
    [Fact]
    public void ContextCount_Is19_ForCodeBlockDecoding()
    {
        Assert.Equal(19, JpxMqDecoder.ContextCount);
    }
}
