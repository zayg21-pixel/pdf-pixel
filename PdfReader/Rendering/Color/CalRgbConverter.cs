using System;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Icc;
using System.Numerics;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Converter for CalRGB (CIEBasedABC) color space.
    /// Builds a synthetic ICC RGB (matrix/TRC + chromatic adaptation) profile
    /// from CalRGB parameters and delegates to IccRgbColorConverter.
    /// NOTE: The PDF /BlackPoint entry for CalRGB is intentionally ignored here.
    /// Empirical testing shows Adobe Acrobat disregards CalRGB /BlackPoint and
    /// treats the space as defined only by WhitePoint, Gamma(s) and Matrix.
    /// Omitting BlackPoint keeps this implementation a canonical reference
    /// matching Acrobat behavior; adding a BlackPoint often produces visible
    /// tonal lifting not seen in Acrobat.
    /// </summary>
    internal sealed class CalRgbConverter : PdfColorSpaceConverter
    {
        private readonly IccRgbColorConverter _iccRgb;

        public CalRgbConverter(
            float xw, float yw, float zw,
            float xb, float yb, float zb, // retained for completeness but unused (BlackPoint ignored)
            float gr, float gg, float gb,
            float[,] matrix)
        {
            // Normalize inputs (PDF spec: Yw must be 1.0; fall back to approximate D65 if missing)
            float Xw = xw <= 0 ? 0.9505f : xw;
            float Yw = yw <= 0 ? 1.0f : yw;
            float Zw = zw <= 0 ? 1.0890f : zw;

            float gR = gr <= 0 ? 1.0f : gr;
            float gG = gg <= 0 ? 1.0f : gg;
            float gB = gb <= 0 ? 1.0f : gb;

            float[,] m = matrix ?? new float[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            };

            // Primaries (PDF defines Matrix row-major -> rows become r,g,b XYZ)
            var primaries = new Vector3[3]
            {
                new Vector3(m[0,0], m[0,1], m[0,2]),
                new Vector3(m[1,0], m[1,1], m[1,2]),
                new Vector3(m[2,0], m[2,1], m[2,2])
            };

            // Bradford adaptation from CalRGB white to D50 (ICC PCS white)
            var chad = IccProfileHelpers.CreateBradfordAdaptMatrix(Xw, Yw, Zw, 0.9642f, 1.0000f, 0.8249f);

            // Build synthetic ICC profile WITHOUT BlackPoint (see class comment)
            var profile = new IccProfile
            {
                Header = new IccProfileHeader
                {
                    ColorSpace = IccConstants.SpaceRgb,
                    Pcs = IccConstants.TypeXYZ,
                    RenderingIntent = 1 // Relative colorimetric default
                },
                WhitePoint = new IccXyz(0.9642f, 1.0f, 0.8249f),
                BlackPoint = null, // Explicitly null: CalRGB /BlackPoint ignored for Acrobat parity
                RedMatrix = new IccXyz(primaries[0].X, primaries[0].Y, primaries[0].Z),
                GreenMatrix = new IccXyz(primaries[1].X, primaries[1].Y, primaries[1].Z),
                BlueMatrix = new IccXyz(primaries[2].X, primaries[2].Y, primaries[2].Z),
                RedTrc = IccTrc.FromGamma(gR),
                GreenTrc = IccTrc.FromGamma(gG),
                BlueTrc = IccTrc.FromGamma(gB),
                ChromaticAdaptation = chad
            };

            _iccRgb = new IccRgbColorConverter(profile);
        }

        public override int Components => 3;

        public override bool IsDevice => false;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            if (!_iccRgb.TryToSrgb01(comps01, renderingIntent, out var srgb01))
            {
                return new SKColor(0, 0, 0, 255);
            }

            byte R = ToByte(srgb01.X);
            byte G = ToByte(srgb01.Y);
            byte B = ToByte(srgb01.Z);
            return new SKColor(R, G, B, 255);
        }
    }
}
