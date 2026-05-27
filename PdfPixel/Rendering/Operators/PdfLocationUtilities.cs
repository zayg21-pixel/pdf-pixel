using PdfPixel.Models;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Utility methods for PDF location related operations and operand handling.
/// </summary>
public static class PdfLocationUtilities
{
    /// <summary>
    /// Create an SKRect from a PDF bounding box array.
    /// Returns null if the array is not defined or has insufficient elements.
    /// </summary>
    /// <param name="bboxArray">Bounding box.</param>
    /// <returns></returns>
    public static SKRect? CreateBBox(PdfArray? bboxArray)
    {
        if (bboxArray == null || bboxArray.Count < 4)
        {
            return null;
        }

        float x0 = bboxArray.GetFloatOrDefault(0);
        float y0 = bboxArray.GetFloatOrDefault(1);
        float x1 = bboxArray.GetFloatOrDefault(2);
        float y1 = bboxArray.GetFloatOrDefault(3);

        return new SKRect(x0, y0, x1, y1).Standardized;
    }

    /// <summary>
    /// Create an SKMatrix from PDF transformation matrix operands (legacy list form).
    /// </summary>
    public static SKMatrix CreateMatrix(List<IPdfValue> operands)
    {
        if (operands == null || operands.Count < 6)
        {
            return SKMatrix.Identity;
        }

        float a = operands[0].AsFloat();
        float b = operands[1].AsFloat();
        float c = operands[2].AsFloat();
        float d = operands[3].AsFloat();
        float e = operands[4].AsFloat();
        float f = operands[5].AsFloat();

        SKMatrix result = new(
            a,
            c,
            e,
            b,
            d,
            f,
            0,
            0,
            1);

        return result;
    }

    /// <summary>
    /// Create an SKMatrix from a strongly-typed PdfArray of operands.
    /// Returns null if the array is or not defined.
    /// </summary>
    public static SKMatrix? CreateMatrix(PdfArray? operands)
    {
        if (operands == null || operands.Count < 6)
        {
            return null;
        }

        float a = operands.GetFloatOrDefault(0);
        float b = operands.GetFloatOrDefault(1);
        float c = operands.GetFloatOrDefault(2);
        float d = operands.GetFloatOrDefault(3);
        float e = operands.GetFloatOrDefault(4);
        float f = operands.GetFloatOrDefault(5);

        SKMatrix result = new(
            a,
            c,
            e,
            b,
            d,
            f,
            0,
            0,
            1);

        return result;
    }
}
