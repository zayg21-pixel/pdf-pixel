using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Factory for creating appropriate JPX tile decoders based on header characteristics.
/// Routes to the general JPX tile decoder which handles all JPEG2000 features.
/// </summary>
internal static class JpxTileDecoderFactory
{
    /// <summary>
    /// Creates the most appropriate tile decoder for the given JPX header.
    /// </summary>
    /// <param name="header">JPX header containing coding parameters.</param>
    /// <returns>The best available decoder for this JPX image.</returns>
    public static IJpxTileDecoder CreateDecoder(JpxHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.CodingStyle == null)
        {
            throw new NotImplementedException("JPX images without coding style information are not supported.");
        }

        // Raw decoder for zero decomposition levels (no wavelet transform)
        if (header.CodingStyle.DecompositionLevels == 0)
        {
            return new JpxRawDecoder(header);
        }

        // General JPX decoder for all wavelet-based images
        // Create packet parser for this header
        var progressionOrder = (JpxProgressionOrder)header.CodingStyle.ProgressionOrder;
        var packetParser = JpxPacketParserFactory.CreateParser(progressionOrder, header);
        
        // This decoder implements the complete JPEG2000 pipeline:
        // Entropy Decoding ? Coefficient Assembly ? Inverse Wavelet Transform
        return new JpxTileDecoder(header, packetParser);
    }
}