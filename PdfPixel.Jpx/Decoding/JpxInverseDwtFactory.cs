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

        JpxQuantization? quantization = header.GetComponentQuantization(componentIndex);

        if (quantization == null)
        {
            throw new InvalidOperationException("Quantization is not defined.");
        }

        // TODO: [HIGH] Take the transform from the component's COC override when it has one.
        // This always reads the main header's COD, so a component whose COC gives it the other
        // wavelet is reconstructed with the wrong one.
        if (header.CodingStyle.IsReversibleTransform)
        {
            return new JpxInverseDwt53(quantization);
        }

        int bitDepth = header.Components[componentIndex].PrecisionBits;
        return new JpxInverseDwt97(quantization, bitDepth);
    }
}
