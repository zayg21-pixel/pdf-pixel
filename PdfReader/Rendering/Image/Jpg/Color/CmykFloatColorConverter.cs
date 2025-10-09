using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Decoding;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// Vectorized CMYK to RGB converter operating in-place on upsampled component blocks.
    /// Input component ordering assumed: C, M, Y, K (standard JPEG CMYK / Adobe transform 0).
    /// After conversion the first three component arrays (0,1,2) hold R,G,B (clamped 0..255 floats).
    /// The K block array is left unchanged (caller / packer can ignore or utilize for additional processing).
    /// </summary>
    internal sealed class CmykFloatColorConverter : IJpgColorConverter
    {
        private static readonly Vector4 VectorZero = Vector4.Zero;
        private static readonly Vector4 Vector255 = new Vector4(255f);
        private static readonly Vector4 VectorOne = new Vector4(1f);
        private static readonly Vector4 Vector255Inv = new Vector4(1f / 255f);

        private readonly JpgDecodingParameters _parameters;

        public CmykFloatColorConverter(JpgHeader header, JpgDecodingParameters parameters)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            if (header.ComponentCount != 4)
            {
                throw new ArgumentException("CMYK converter requires exactly 4 components.", nameof(header));
            }
            _parameters = parameters;
        }

        public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
        {
            if (upsampledBandBlocks == null)
            {
                throw new ArgumentNullException(nameof(upsampledBandBlocks));
            }
            if (upsampledBandBlocks.Length < 4)
            {
                throw new ArgumentException("CMYK converter requires 4 component arrays.", nameof(upsampledBandBlocks));
            }

            Block8x8F[] cBlocks = upsampledBandBlocks[0];
            Block8x8F[] mBlocks = upsampledBandBlocks[1];
            Block8x8F[] yBlocks = upsampledBandBlocks[2];
            Block8x8F[] kBlocks = upsampledBandBlocks[3]; // Preserved (not overwritten)

            int totalBlocks = cBlocks.Length;
            for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
            {
                ref Block8x8F cBlock = ref cBlocks[blockIndex];
                ref Block8x8F mBlock = ref mBlocks[blockIndex];
                ref Block8x8F yBlock = ref yBlocks[blockIndex];
                ref Block8x8F kBlock = ref kBlocks[blockIndex];

                // Obtain references to first vector lane of each 8x8 block (16 lanes total) so we can stride with Unsafe.Add.
                ref Vector4 cVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref cBlock);
                ref Vector4 mVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref mBlock);
                ref Vector4 yVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref yBlock);
                ref Vector4 kVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref kBlock);

                for (int vectorIndex = 0; vectorIndex < Block8x8F.VectorCount; vectorIndex++)
                {
                    // Normalize CMYK components from 0..255 to 0..1 range.
                    Vector4 c = Unsafe.Add(ref cVecRef, vectorIndex) * Vector255Inv;
                    Vector4 m = Unsafe.Add(ref mVecRef, vectorIndex) * Vector255Inv;
                    Vector4 y = Unsafe.Add(ref yVecRef, vectorIndex) * Vector255Inv;
                    Vector4 k = Unsafe.Add(ref kVecRef, vectorIndex) * Vector255Inv;

                    Vector4 oneMinusC = VectorOne - c;
                    Vector4 oneMinusM = VectorOne - m;
                    Vector4 oneMinusY = VectorOne - y;
                    Vector4 oneMinusK = VectorOne - k;

                    // Standard CMYK to RGB conversion: R = 255 * (1 - C) * (1 - K), etc.
                    Vector4 r = oneMinusC * oneMinusK * Vector255;
                    Vector4 g = oneMinusM * oneMinusK * Vector255;
                    Vector4 b = oneMinusY * oneMinusK * Vector255;

                    Unsafe.Add(ref cVecRef, vectorIndex) = Vector4.Clamp(r, VectorZero, Vector255); // Overwrite C with R
                    Unsafe.Add(ref mVecRef, vectorIndex) = Vector4.Clamp(g, VectorZero, Vector255); // Overwrite M with G
                    Unsafe.Add(ref yVecRef, vectorIndex) = Vector4.Clamp(b, VectorZero, Vector255); // Overwrite Y with B
                }
            }
        }
    }
}
