using PdfPixel.Jpg.Model;

using System;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Creates the row decoder implementation matching the frame type of a parsed JPEG header.
/// </summary>
public static class JpgDecoderFactory
{
    /// <summary>
    /// Creates a row decoder for the frame described by <paramref name="header"/>.
    /// </summary>
    /// <param name="header">Parsed JPEG header from <see cref="Readers.JpgReader.ParseHeader"/>.</param>
    /// <param name="encodedData">Complete JPEG data the header was parsed from.</param>
    /// <param name="options">Optional decoding overrides; uses <see cref="JpgDecoderOptions.Default"/> when null.</param>
    /// <returns>A decoder positioned at the first image row.</returns>
    public static IJpgDecoder Create(JpgHeader header, in ReadOnlyMemory<byte> encodedData, JpgDecoderOptions? options = null)
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
            JpgFrameType.ProgressiveDct => new JpgProgressiveDecoder(header, entropyData, options),
            JpgFrameType.BaselineDct or JpgFrameType.ExtendedSequentialDct => new JpgBaselineDecoder(header, entropyData, options),
            _ => throw new NotSupportedException($"JPEG frame type {header.FrameType.ToString()} is not supported.")
        };
    }
}
