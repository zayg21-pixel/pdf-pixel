using PdfReader.Models;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Utility methods for matrix operations and operand handling
    /// Focused on mathematical operations for PDF rendering
    /// </summary>
    public static class PdfMatrixUtilities
    {
        /// <summary>
        /// Create an SKMatrix from PDF transformation matrix operands (legacy list form).
        /// </summary>
        public static SKMatrix CreateMatrix(List<IPdfValue> operands)
        {
            if (operands == null || operands.Count < 6)
            {
                return SKMatrix.Identity;
            }

            var a = operands[0].AsFloat();
            var b = operands[1].AsFloat();
            var c = operands[2].AsFloat();
            var d = operands[3].AsFloat();
            var e = operands[4].AsFloat();
            var f = operands[5].AsFloat();

            var result = new SKMatrix(
                a, c, e,
                b, d, f,
                0, 0, 1);

            return result;
        }

        /// <summary>
        /// Create an SKMatrix from a strongly-typed PdfArray of operands.
        /// </summary>
        public static SKMatrix CreateMatrix(PdfArray operands)
        {
            if (operands == null || operands.Count < 6)
            {
                return SKMatrix.Identity;
            }

            float a = operands.GetFloat(0);
            float b = operands.GetFloat(1);
            float c = operands.GetFloat(2);
            float d = operands.GetFloat(3);
            float e = operands.GetFloat(4);
            float f = operands.GetFloat(5);

            var result = new SKMatrix(
                a, c, e,
                b, d, f,
                0, 0, 1);

            return result;
        }
    }
}