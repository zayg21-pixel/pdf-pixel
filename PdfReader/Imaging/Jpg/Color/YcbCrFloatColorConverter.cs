using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using PdfReader.Imaging.Jpg.Decoding;
using PdfReader.Imaging.Jpg.Model;

namespace PdfReader.Imaging.Jpg.Color;

/// <summary>
/// Float vectorized YCbCr to RGB converter working on already upsampled 8x8 float blocks.
/// Performs in-place YCbCr->RGB conversion (writes R,G,B back into the original component block slots) then clamps to byte range.
/// Immutable header-derived metadata captured at construction to avoid repeated argument passing.
/// </summary>
internal sealed class YcbCrFloatColorConverter : IJpgColorConverter
{
    private static readonly Vector4 VectorZero = Vector4.Zero;
    private static readonly Vector4 Vector255 = new Vector4(255f);
    private static readonly Vector4 Offset = new Vector4(128f);
    private static readonly Vector4 CrToR = new Vector4(1.402f);
    private static readonly Vector4 CbToG = new Vector4(0.344136f);
    private static readonly Vector4 CrToG = new Vector4(0.714136f);
    private static readonly Vector4 CbToB = new Vector4(1.772f);

    private readonly JpgDecodingParameters _parameters;

    public YcbCrFloatColorConverter(JpgHeader header, JpgDecodingParameters parameters)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }
        if (header.ComponentCount != 3)
        {
            throw new ArgumentException("YCbCr converter requires exactly 3 components.", nameof(header));
        }
        _parameters = parameters;
    }

    public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
    {
        if (upsampledBandBlocks == null)
        {
            throw new ArgumentNullException(nameof(upsampledBandBlocks));
        }
        if (upsampledBandBlocks.Length < 3)
        {
            throw new ArgumentException("YCbCr converter requires 3 component arrays.", nameof(upsampledBandBlocks));
        }

        Block8x8F[] yBlocks = upsampledBandBlocks[0];
        Block8x8F[] cbBlocks = upsampledBandBlocks[1];
        Block8x8F[] crBlocks = upsampledBandBlocks[2];
        int totalBlocks = yBlocks.Length;

        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            ref Block8x8F yBlock = ref yBlocks[blockIndex];
            ref Block8x8F cbBlock = ref cbBlocks[blockIndex];
            ref Block8x8F crBlock = ref crBlocks[blockIndex];

            // Take a ref to the first Vector4 lane of each block so we can index subsequent lanes with Unsafe.Add.
            ref Vector4 yVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref yBlock);
            ref Vector4 cbVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref cbBlock);
            ref Vector4 crVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref crBlock);

            for (int vectorIndex = 0; vectorIndex < Block8x8F.VectorCount; vectorIndex++)
            {
                Vector4 yVec = Unsafe.Add(ref yVecRef, vectorIndex);
                Vector4 cbVec = Unsafe.Add(ref cbVecRef, vectorIndex);
                Vector4 crVec = Unsafe.Add(ref crVecRef, vectorIndex);

                Vector4 cbCentered = cbVec - Offset;
                Vector4 crCentered = crVec - Offset;

                Vector4 r = yVec + crCentered * CrToR;
                Vector4 g = yVec - cbCentered * CbToG - crCentered * CrToG;
                Vector4 b = yVec + cbCentered * CbToB;

                // Store clamped results directly back into the original component blocks (now representing R,G,B).
                Unsafe.Add(ref yVecRef, vectorIndex) = Vector4.Clamp(r, VectorZero, Vector255);
                Unsafe.Add(ref cbVecRef, vectorIndex) = Vector4.Clamp(g, VectorZero, Vector255);
                Unsafe.Add(ref crVecRef, vectorIndex) = Vector4.Clamp(b, VectorZero, Vector255);
            }
        }
    }
}
