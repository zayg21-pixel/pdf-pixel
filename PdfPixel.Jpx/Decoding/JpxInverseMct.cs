using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Applies inverse multi-component transform (MCT) to reconstructed tile components.
/// Supports both RCT (reversible, for 5-3 wavelet) and ICT (irreversible, for 9-7 wavelet)
/// per ITU-T T.800 Annex G.
/// </summary>
internal sealed class JpxInverseMct
{
    private readonly bool _isReversible;
    private readonly byte _mctType;

    /// <summary>
    /// Creates an inverse MCT processor for the specified coding style.
    /// </summary>
    /// <param name="codingStyle">Coding style parameters from the header.</param>
    public JpxInverseMct(JpxCodingStyle codingStyle)
    {
        if (codingStyle == null)
        {
            throw new ArgumentNullException(nameof(codingStyle));
        }

        _isReversible = codingStyle.IsReversibleTransform;
        _mctType = codingStyle.MultiComponentTransform;
    }

    /// <summary>
    /// Gets whether MCT is enabled for this coding style.
    /// </summary>
    public bool IsEnabled => _mctType != 0;

    /// <summary>
    /// Applies the inverse MCT to tile component data in-place.
    /// Requires at least 3 components; additional components are left unchanged.
    /// </summary>
    /// <param name="tile">Tile with reconstructed component data to transform.</param>
    public void Apply(JpxTile tile)
    {
        if (tile == null)
        {
            throw new ArgumentNullException(nameof(tile));
        }

        // The transform reads the first three components together, so it can only run when all
        // three were reconstructed.
        if (!IsEnabled || tile.ComponentCount < 3)
        {
            return;
        }

        if (!tile.IsComponentDecoded(0) || !tile.IsComponentDecoded(1) || !tile.IsComponentDecoded(2))
        {
            return;
        }

        int[] comp0 = tile.ComponentData[0];
        int[] comp1 = tile.ComponentData[1];
        int[] comp2 = tile.ComponentData[2];
        int pixelCount = tile.SampleCount;

        if (_isReversible)
        {
            ApplyInverseRct(comp0, comp1, comp2, pixelCount);
        }
        else
        {
            ApplyInverseIct(comp0, comp1, comp2, pixelCount);
        }
    }

    /// <summary>
    /// Inverse RCT (ITU-T T.800 Annex G.2).
    /// G = Y - floor((Cb + Cr) / 4), R = Cr + G, B = Cb + G.
    /// </summary>
    private static void ApplyInverseRct(int[] comp0, int[] comp1, int[] comp2, int pixelCount)
    {
        for (int i = 0; i < pixelCount; i++)
        {
            int y = comp0[i];
            int cb = comp1[i];
            int cr = comp2[i];
            int g = y - ((cb + cr) >> 2);
            comp0[i] = cr + g;
            comp1[i] = g;
            comp2[i] = cb + g;
        }
    }

    /// <summary>
    /// Inverse ICT (ITU-T T.800 Annex G.1) using 13-bit fixed-point integer arithmetic.
    /// R = Y + 1.402 * Cr, G = Y - 0.34413 * Cb - 0.71414 * Cr, B = Y + 1.772 * Cb.
    /// </summary>
    private static void ApplyInverseIct(int[] comp0, int[] comp1, int[] comp2, int pixelCount)
    {
        // 13-bit fixed-point coefficients
        const int FracBits = 13;
        const int Half = 1 << (FracBits - 1);
        const int Cr_R = 11485;   // round(1.402 * 8192)
        const int Cb_G = -2819;   // round(-0.34413 * 8192)
        const int Cr_G = -5850;   // round(-0.71414 * 8192)
        const int Cb_B = 14516;   // round(1.772 * 8192)

        for (int i = 0; i < pixelCount; i++)
        {
            int y = comp0[i];
            int cb = comp1[i];
            int cr = comp2[i];
            comp0[i] = y + (((Cr_R * cr) + Half) >> FracBits);
            comp1[i] = y + (((Cb_G * cb) + (Cr_G * cr) + Half) >> FracBits);
            comp2[i] = y + (((Cb_B * cb) + Half) >> FracBits);
        }
    }
}
