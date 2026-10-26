using System;

namespace PdfPixel.Jpeg;

/// <summary>
/// Creates the row decoder implementation matching the frame type of a parsed JPEG header.
/// </summary>
public static class JpegDecoderFactory
{
    /// <summary>
    /// Creates a row decoder for the frame described by <paramref name="header"/>.
    /// </summary>
    /// <param name="header">Parsed JPEG header from <see cref="JpegHeader.Parse"/>.</param>
    /// <param name="encodedData">Complete JPEG data the header was parsed from.</param>
    /// <param name="options">Optional decoding overrides; uses <see cref="JpegDecoderOptions.Default"/> when null.</param>
    /// <returns>A decoder positioned at the first image row.</returns>
    public static IJpegDecoder Create(JpegHeader header, in ReadOnlyMemory<byte> encodedData, JpegDecoderOptions? options = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.ContentOffset < 0 || header.ContentOffset > encodedData.Length)
        {
            throw new ArgumentException("Header does not point at entropy-coded data.", nameof(header));
        }

        ReadOnlyMemory<byte> entropyData = encodedData.Slice(header.ContentOffset);

        return header.FrameType switch
        {
            JpegFrameType.ProgressiveDct => new JpegProgressiveDecoder(header, entropyData, options),
            JpegFrameType.BaselineDct or JpegFrameType.ExtendedSequentialDct => new JpegBaselineDecoder(header, entropyData, options),
            _ => throw new NotSupportedException($"JPEG frame type {header.FrameType.ToString()} is not supported.")
        };
    }
}
