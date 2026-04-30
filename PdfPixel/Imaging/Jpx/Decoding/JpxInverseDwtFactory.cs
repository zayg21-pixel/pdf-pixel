using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Factory for creating the appropriate inverse DWT implementation
/// based on the coding style parameters.
/// </summary>
internal static class JpxInverseDwtFactory
{
    /// <summary>
    /// Creates the appropriate inverse DWT instance for a component.
    /// </summary>
    /// <param name="header">JPX header with coding and quantization parameters.</param>
    /// <param name="componentIndex">Component index for bit depth lookup.</param>
    /// <returns>An <see cref="IJpxInverseDwt"/> instance configured for the transform type.</returns>
    public static IJpxInverseDwt Create(JpxHeader header, int componentIndex)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.CodingStyle.IsReversibleTransform)
        {
            return new JpxInverseDwt53(header.Quantization, header.CodingStyle.DecompositionLevels);
        }

        int bitDepth = header.Components[componentIndex].PrecisionBits;
        return new JpxInverseDwt97(header.Quantization, bitDepth);
    }
}
