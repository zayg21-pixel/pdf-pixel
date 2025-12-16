using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using PdfReader.Imaging.Jpg.Decoding;
using PdfReader.Imaging.Jpg.Model;

namespace PdfReader.Imaging.Jpg.Color;

/// <summary>
/// In-place YCCK to CMYK converter. Input ordering: Y, Cb, Cr, K. Output ordering: C, M, Y, K.
/// Algorithm: Convert YCbCr to provisional RGB, clamp, then map to CMY = 255 - RGB while preserving K.
/// Uses unsafe ref vector iteration for performance (avoids per-lane Get/Set calls).
/// </summary>
internal sealed class YcckFloatColorConverter : IJpgColorConverter
{
    private static readonly Vector4 VectorZero = Vector4.Zero;
    private static readonly Vector4 Vector255 = new Vector4(255f);
    private static readonly Vector4 Offset = new Vector4(128f);
    private static readonly Vector4 CrToR = new Vector4(1.402f);
    private static readonly Vector4 CbToG = new Vector4(0.344136f);
    private static readonly Vector4 CrToG = new Vector4(0.714136f);
    private static readonly Vector4 CbToB = new Vector4(1.772f);

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

                Vector4 cbCentered = cbVec - Offset;
                Vector4 crCentered = crVec - Offset;

                Vector4 rVec = yVec + crCentered * CrToR;
                Vector4 gVec = yVec - cbCentered * CbToG - crCentered * CrToG;
                Vector4 bVec = yVec + cbCentered * CbToB;

                rVec = Vector4.Clamp(rVec, VectorZero, Vector255);
                gVec = Vector4.Clamp(gVec, VectorZero, Vector255);
                bVec = Vector4.Clamp(bVec, VectorZero, Vector255);

                // Overwrite source component blocks with CMY derived from provisional RGB.
                Unsafe.Add(ref yVecRef, vectorIndex) = Vector255 - rVec; // C
                Unsafe.Add(ref cbVecRef, vectorIndex) = Vector255 - gVec; // M
                Unsafe.Add(ref crVecRef, vectorIndex) = Vector255 - bVec; // Y
                Unsafe.Add(ref kVecRef, vectorIndex) = Vector4.Clamp(kVec, VectorZero, Vector255); // K

            }
        }
    }
}
