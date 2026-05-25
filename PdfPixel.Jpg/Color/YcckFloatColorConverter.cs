using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Color;

/// <summary>
/// In-place YCCK to CMYK converter. Input ordering: Y, Cb, Cr, K. Output ordering: C, M, Y, K.
/// Algorithm: Convert YCbCr to provisional RGB, clamp, then map to CMY = 255 - RGB while preserving K.
/// Uses unsafe ref vector iteration for performance (avoids per-lane Get/Set calls).
/// </summary>
internal sealed class YcckFloatColorConverter : IJpgColorConverter
{
    private static readonly Vector4 VectorZero = Vector4.Zero;
    private static readonly Vector4 Vector255 = new(255f);
    private static readonly Vector4 OffsetR = new(434.456f);
    private static readonly Vector4 OffsetG = new(119.541f);
    private static readonly Vector4 OffsetB = new(481.816f);
    private static readonly Vector4 CrToR = new(1.402f);
    private static readonly Vector4 CbToG = new(0.344136f);
    private static readonly Vector4 CrToG = new(0.714136f);
    private static readonly Vector4 CbToB = new(1.772f);

    private readonly JpgDecodingParameters _parameters;

    public YcckFloatColorConverter(JpgHeader header, JpgDecodingParameters parameters)
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
            throw new ArgumentException("YCCK converter requires 4 components.", nameof(header));
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
            throw new ArgumentException("YCCK converter requires 4 component arrays.", nameof(upsampledBandBlocks));
        }

        Block8x8F[] yBlocks = upsampledBandBlocks[0];
        Block8x8F[] cbBlocks = upsampledBandBlocks[1];
        Block8x8F[] crBlocks = upsampledBandBlocks[2];
        Block8x8F[] kBlocks = upsampledBandBlocks[3];

        int totalBlocks = yBlocks.Length;
        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            ref Block8x8F yBlock = ref yBlocks[blockIndex];
            ref Block8x8F cbBlock = ref cbBlocks[blockIndex];
            ref Block8x8F crBlock = ref crBlocks[blockIndex];
            ref Block8x8F kBlock = ref kBlocks[blockIndex];

            // Obtain refs to the first Vector4 of each block to stride with Unsafe.Add.
            ref Vector4 yVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref yBlock);
            ref Vector4 cbVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref cbBlock);
            ref Vector4 crVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref crBlock);
            ref Vector4 kVecRef = ref Unsafe.As<Block8x8F, Vector4>(ref kBlock);

            for (int vectorIndex = 0; vectorIndex < Block8x8F.VectorCount; vectorIndex++)
            {
                Vector4 yVec = Unsafe.Add(ref yVecRef, vectorIndex);
                Vector4 cbVec = Unsafe.Add(ref cbVecRef, vectorIndex);
                Vector4 crVec = Unsafe.Add(ref crVecRef, vectorIndex);
                Vector4 kVec = Unsafe.Add(ref kVecRef, vectorIndex);

                Vector4 cRVec = OffsetR - yVec - (CrToR * crVec);
                Vector4 mRVec = OffsetG - yVec + (CbToG * cbVec) + (CrToG * crVec);
                Vector4 yRVec = OffsetB - yVec - (CbToB * cbVec);

                // Overwrite source component blocks with CMY derived from provisional RGB.
                Unsafe.Add(ref yVecRef, vectorIndex) = Vector4.Clamp(cRVec, VectorZero, Vector255); // C
                Unsafe.Add(ref cbVecRef, vectorIndex) = Vector4.Clamp(mRVec, VectorZero, Vector255); // M
                Unsafe.Add(ref crVecRef, vectorIndex) = Vector4.Clamp(yRVec, VectorZero, Vector255); // Y
                Unsafe.Add(ref kVecRef, vectorIndex) = Vector4.Clamp(kVec, VectorZero, Vector255); // K

            }
        }
    }
}
