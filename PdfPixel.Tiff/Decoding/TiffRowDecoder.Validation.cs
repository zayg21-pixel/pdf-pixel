using PdfPixel.Tiff.Model;
using System;
using System.IO;

namespace PdfPixel.Tiff.Decoding;

public sealed partial class TiffRowDecoder
{
    private static int[] ResolveSampleBits(TiffDirectory directory)
    {
        int[] declaredBits = directory.BitsPerSample;
        int samplesPerPixel = directory.SamplesPerPixel;

        if (samplesPerPixel <= 0)
        {
            throw new InvalidDataException($"TIFF image declares {samplesPerPixel} sample(s) per pixel.");
        }

        if (declaredBits.Length == samplesPerPixel)
        {
            return declaredBits;
        }

        if (declaredBits.Length != 1)
        {
            throw new InvalidDataException($"TIFF image declares {declaredBits.Length} bit depth(s) for {samplesPerPixel} sample(s) per pixel.");
        }

        var sampleBits = new int[samplesPerPixel];
        sampleBits.AsSpan().Fill(declaredBits[0]);
        return sampleBits;
    }

    private static void Validate(TiffDirectory directory, int[] sampleBits)
    {
        if (directory.Width <= 0 || directory.Height <= 0)
        {
            throw new InvalidDataException($"TIFF image is {directory.Width}x{directory.Height}.");
        }

        bool isFloat = directory.SampleFormat == TiffSampleFormat.FloatingPoint;
        var isUniform = true;

        for (int sample = 0; sample < sampleBits.Length; sample++)
        {
            int bits = sampleBits[sample];

            // TODO: [LOW] 24-bit floating-point samples. No available writer produces a correct one to test against.
            bool isSupported = isFloat
                ? bits == 16 || bits == 32 || bits == 64
                : bits >= 1 && bits <= MaxBitsPerSample;

            if (!isSupported)
            {
                throw new NotSupportedException($"TIFF {directory.SampleFormat} samples of {bits} bits are not supported.");
            }

            isUniform &= bits == sampleBits[0];
        }

        if (directory.Predictor == TiffPredictor.Horizontal && (!isUniform || !IsPredictorDepth(sampleBits[0])))
        {
            throw new NotSupportedException($"TIFF horizontal predictor over {sampleBits[0]}-bit samples is not supported.");
        }

        if (directory.Predictor == TiffPredictor.FloatingPoint && (!isFloat || !isUniform))
        {
            throw new NotSupportedException("TIFF floating-point predictor requires floating-point samples of one depth.");
        }

        if (directory.Photometric == TiffPhotometric.YCbCr && !IsJpeg(directory.Compression))
        {
            ValidateYCbCr(directory, sampleBits, isUniform);
        }

        if (directory.Compression == TiffCompression.OldJpeg)
        {
            ValidateOldJpeg(directory, sampleBits, isUniform);
        }

        bool isCcitt = directory.Compression == TiffCompression.CcittRle
            || directory.Compression == TiffCompression.CcittGroup3
            || directory.Compression == TiffCompression.CcittGroup4;

        if (isCcitt && (sampleBits[0] != 1 || sampleBits.Length != 1))
        {
            throw new InvalidDataException("TIFF CCITT compression requires 1-bit single-sample pixels.");
        }

        if (directory.Compression == TiffCompression.Jpeg && (!isUniform || sampleBits[0] != 8))
        {
            throw new NotSupportedException("TIFF JPEG compression of samples other than 8 bits is not supported.");
        }
    }

    private static void ValidateYCbCr(TiffDirectory directory, int[] sampleBits, bool isUniform)
    {
        if (sampleBits.Length != 3 || !isUniform || sampleBits[0] != 8)
        {
            throw new NotSupportedException("TIFF YCbCr samples other than three 8-bit samples are not supported.");
        }

        if (directory.PlanarConfiguration == TiffPlanarConfiguration.Planar)
        {
            int[] planarSubsampling = directory.YCbCrSubsampling ?? DefaultYCbCrSubsampling;
            if (planarSubsampling[0] != 1 || planarSubsampling[1] != 1)
            {
                // TODO: [LOW] Planar YCbCr with subsampled chroma planes.
                throw new NotSupportedException("TIFF planar YCbCr with subsampled chroma is not supported.");
            }

            return;
        }

        int[] subsampling = directory.YCbCrSubsampling ?? DefaultYCbCrSubsampling;
        if (subsampling.Length != 2 || !IsSubsamplingFactor(subsampling[0]) || !IsSubsamplingFactor(subsampling[1]) || subsampling[1] > subsampling[0])
        {
            throw new InvalidDataException("TIFF YCbCrSubSampling must be 1, 2 or 4, vertical no larger than horizontal.");
        }

        if (directory.Predictor != TiffPredictor.NoPrediction)
        {
            throw new NotSupportedException("TIFF predictor over YCbCr data units is not supported.");
        }
    }

    private static void ValidateOldJpeg(TiffDirectory directory, int[] sampleBits, bool isUniform)
    {
        if (!isUniform || sampleBits[0] != 8)
        {
            throw new NotSupportedException("TIFF old-style JPEG of samples other than 8 bits is not supported.");
        }

        if (directory.PlanarConfiguration == TiffPlanarConfiguration.Planar && sampleBits.Length > 1)
        {
            throw new NotSupportedException("TIFF planar old-style JPEG is not supported.");
        }

        if (directory.OldJpeg != null && directory.OldJpeg.Process != 1)
        {
            // TODO: [LOW] Old-style lossless JPEG (JPEGProc 14), which needs a lossless decoder in PdfPixel.Jpg.
            throw new NotSupportedException($"TIFF old-style JPEG process {directory.OldJpeg.Process} is not supported.");
        }
    }

    private static bool IsJpeg(TiffCompression compression) => compression == TiffCompression.Jpeg || compression == TiffCompression.OldJpeg;

    private static bool IsSubsamplingFactor(int factor) => factor == 1 || factor == 2 || factor == 4;

    private static bool IsPredictorDepth(int bits) => bits == 1 || bits == 2 || bits == 4 || bits == 8 || bits == 16 || bits == 32;
}
