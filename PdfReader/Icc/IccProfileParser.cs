using System;
using System.Collections.Generic;

namespace PdfReader.Icc
{
    /// <summary>
    /// Entry point for ICC parsing in the PDF reader. Focused on features relevant to PDF:
    /// - Accept N=1/3/4 profiles (Gray/RGB/CMYK). DeviceLink/NamedColor are out of scope.
    /// - Parse header and minimal tag directory.
    /// - Extract matrix/TRC data for Gray/RGB.
    /// - Detect presence of LUT-based A2B0/B2A0 for CMYK (but do not parse yet).
    /// </summary>
    internal static class IccProfileParser
    {
        public static IccProfile TryParse(byte[] iccBytes)
        {
            if (iccBytes == null || iccBytes.Length < 132) return null;
            try
            {
                return IccProfile.Parse(iccBytes);
            }
            catch
            {
                // TODO: diagnostics/logging if needed
                return null;
            }
        }

        public static bool IsGray(IccProfile p) => p != null && string.Equals(p.Header.ColorSpace, IccConstants.SpaceGray, StringComparison.Ordinal);
        public static bool IsRgb(IccProfile p) => p != null && string.Equals(p.Header.ColorSpace, IccConstants.SpaceRgb, StringComparison.Ordinal);
        public static bool IsCmyk(IccProfile p) => p != null && string.Equals(p.Header.ColorSpace, IccConstants.SpaceCmyk, StringComparison.Ordinal);

        public static float[] GetWhitePointXYZ(IccProfile p)
        {
            if (p?.WhitePoint != null)
            {
                return new[] { p.WhitePoint.Value.X, p.WhitePoint.Value.Y, p.WhitePoint.Value.Z };
            }
            // Fallback to header illuminant if wtpt missing (common in v2)
            if (p != null && p.Header.Illuminant != null)
            {
                return new[] { p.Header.Illuminant.Value.X, p.Header.Illuminant.Value.Y, p.Header.Illuminant.Value.Z };
            }
            return null;
        }

        public static (float[,] m, float[] gamma)? GetRgbMatrixTrc(IccProfile p)
        {
            if (p == null || !IsRgb(p)) return null;
            if (p.RedMatrix != null && p.GreenMatrix != null && p.BlueMatrix != null && p.RedTrc != null && p.GreenTrc != null && p.BlueTrc != null)
            {
                var m = new float[3, 3];
                m[0, 0] = p.RedMatrix.Value.X;   m[0, 1] = p.GreenMatrix.Value.X; m[0, 2] = p.BlueMatrix.Value.X;
                m[1, 0] = p.RedMatrix.Value.Y;   m[1, 1] = p.GreenMatrix.Value.Y; m[1, 2] = p.BlueMatrix.Value.Y;
                m[2, 0] = p.RedMatrix.Value.Z;   m[2, 1] = p.GreenMatrix.Value.Z; m[2, 2] = p.BlueMatrix.Value.Z;

                // Use per-channel gamma only when TRC is a simple gamma curve. Full TRCs (sampled/parametric)
                // are consumed downstream by the converter via p.RedTrc/GreenTrc/BlueTrc.
                float gr = p.RedTrc.IsGamma ? p.RedTrc.Gamma : 1f;
                float gg = p.GreenTrc.IsGamma ? p.GreenTrc.Gamma : 1f;
                float gb = p.BlueTrc.IsGamma ? p.BlueTrc.Gamma : 1f;
                return (m, new[] { gr, gg, gb });
            }

            // If LUT-based, prefer A2B in higher-level code paths
            return null;
        }

        public static float? GetGrayGamma(IccProfile p)
        {
            if (p == null || !IsGray(p)) return null;
            if (p.GrayTrc != null)
            {
                if (p.GrayTrc.IsGamma) return p.GrayTrc.Gamma;
                // Full gray TRC handled downstream
            }
            return null;
        }

        public static IccLutPipeline GetA2BLut(IccProfile p)
        {
            // Choose LUT by rendering intent (0..3), fallback order: specific -> 0 -> 1 -> 2 -> any non-null
            if (p == null) return null;
            int intent = (int)(p.Header?.RenderingIntent ?? 0);
            switch (intent)
            {
                case 0: return p.A2BLut0 ?? p.A2BLut1 ?? p.A2BLut2; // Perceptual
                case 1: return p.A2BLut1 ?? p.A2BLut0 ?? p.A2BLut2; // Media-relative colorimetric
                case 2: return p.A2BLut2 ?? p.A2BLut0 ?? p.A2BLut1; // Saturation
                case 3: return p.A2BLut1 ?? p.A2BLut0 ?? p.A2BLut2; // Absolute (use relative if abs missing)
                default: return p.A2BLut0 ?? p.A2BLut1 ?? p.A2BLut2;
            }
        }
    }
}
