using PdfPixel.Tiff.Model;
using System;

namespace PdfPixel.Tiff.Decoding;

/// <summary>
/// Converts 8-bit YCbCr samples to RGB using the luma coefficients and reference black and white codes of
/// a TIFF image, in the fixed-point arithmetic of libtiff's TIFFYCbCrtoRGB.
/// </summary>
internal sealed class TiffYCbCrConverter
{
    private const int Shift = 16;
    private const int OneHalf = 1 << (Shift - 1);
    private const float TableClamp = 128 * 32;

    private static readonly double[] DefaultYCbCrCoefficients = [0.299, 0.587, 0.114];
    private static readonly double[] DefaultReferenceBlackWhite = [0, 255, 128, 255, 128, 255];

    private readonly int[] _lumaTable = new int[256];
    private readonly int[] _redFromCrTable = new int[256];
    private readonly int[] _blueFromCbTable = new int[256];
    private readonly int[] _greenFromCrTable = new int[256];
    private readonly int[] _greenFromCbTable = new int[256];

    /// <summary>
    /// Builds the conversion tables from a directory's YCbCrCoefficients and ReferenceBlackWhite, or their TIFF defaults.
    /// </summary>
    /// <param name="directory">Directory of a YCbCr image.</param>
    public TiffYCbCrConverter(TiffDirectory directory)
        : this(directory.YCbCrCoefficients ?? DefaultYCbCrCoefficients, directory.ReferenceBlackWhite ?? DefaultReferenceBlackWhite)
    {
    }

    /// <summary>
    /// Builds the conversion tables.
    /// </summary>
    /// <param name="lumaCoefficients">Luma coefficients of red, green and blue.</param>
    /// <param name="referenceBlackWhite">Black and white reference codes of Y, Cb and Cr.</param>
    public TiffYCbCrConverter(double[] lumaCoefficients, double[] referenceBlackWhite)
    {
        var lumaRed = (float)lumaCoefficients[0];
        var lumaGreen = (float)lumaCoefficients[1];
        var lumaBlue = (float)lumaCoefficients[2];

        float redFactor = 2 - (2 * lumaRed);
        float greenFromRedFactor = lumaRed * redFactor / lumaGreen;
        float blueFactor = 2 - (2 * lumaBlue);
        float greenFromBlueFactor = lumaBlue * blueFactor / lumaGreen;

        int redFromCr = ToFixed(Clamp(redFactor, 0, 2));
        int greenFromCr = -ToFixed(Clamp(greenFromRedFactor, 0, 2));
        int blueFromCb = ToFixed(Clamp(blueFactor, 0, 2));
        int greenFromCb = -ToFixed(Clamp(greenFromBlueFactor, 0, 2));

        var lumaBlack = (float)referenceBlackWhite[0];
        var lumaWhite = (float)referenceBlackWhite[1];
        float blueBlack = (float)referenceBlackWhite[2] - 128;
        float blueWhite = (float)referenceBlackWhite[3] - 128;
        float redBlack = (float)referenceBlackWhite[4] - 128;
        float redWhite = (float)referenceBlackWhite[5] - 128;

        for (int index = 0; index < 256; index++)
        {
            int code = index - 128;
            var cr = (int)Clamp(CodeToValue(code, redBlack, redWhite, 127), -TableClamp, TableClamp);
            var cb = (int)Clamp(CodeToValue(code, blueBlack, blueWhite, 127), -TableClamp, TableClamp);

            _redFromCrTable[index] = ((redFromCr * cr) + OneHalf) >> Shift;
            _blueFromCbTable[index] = ((blueFromCb * cb) + OneHalf) >> Shift;
            _greenFromCrTable[index] = greenFromCr * cr;
            _greenFromCbTable[index] = (greenFromCb * cb) + OneHalf;
            _lumaTable[index] = (int)Clamp(CodeToValue(code + 128, lumaBlack, lumaWhite, 255), -TableClamp, TableClamp);
        }
    }

    /// <summary>
    /// Writes the red, green and blue samples of one pixel into the first three bytes of <paramref name="rgb"/>.
    /// </summary>
    public void Convert(byte luma, byte blueDifference, byte redDifference, in Span<byte> rgb)
    {
        int lumaValue = _lumaTable[luma];

        rgb[0] = ClampToByte(lumaValue + _redFromCrTable[redDifference]);
        rgb[1] = ClampToByte(lumaValue + ((_greenFromCbTable[blueDifference] + _greenFromCrTable[redDifference]) >> Shift));
        rgb[2] = ClampToByte(lumaValue + _blueFromCbTable[blueDifference]);
    }

    private static float CodeToValue(int code, float referenceBlack, float referenceWhite, float codeRange)
    {
        float range = referenceWhite - referenceBlack;
        return (code - (int)referenceBlack) * codeRange / ((range != 0) ? range : 1);
    }

    private static int ToFixed(float value) => (int)((value * (1L << Shift)) + 0.5);

    private static float Clamp(float value, float minimum, float maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        return (value > maximum) ? maximum : value;
    }

    private static byte ClampToByte(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return (value > byte.MaxValue) ? byte.MaxValue : (byte)value;
    }
}
