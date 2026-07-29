using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Decoding;

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

        if (header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        if (header.Quantization == null)
        {
            throw new InvalidOperationException("Quantization is not defined.");
        }

        // TODO: [HIGH] Use the component's QCC override when it has one. This always takes the
        // main header's QCD, so a component given its own step sizes is dequantized with the
        // wrong ones. JpxHeader.ComponentQuantizations is parsed but never read. The same
        // applies to the transform, which a COC override can change per component.
        if (header.CodingStyle.IsReversibleTransform)
        {
            return new JpxInverseDwt53(header.Quantization);
        }

        int bitDepth = header.Components[componentIndex].PrecisionBits;
        return new JpxInverseDwt97(header.Quantization, bitDepth);
    }
}
