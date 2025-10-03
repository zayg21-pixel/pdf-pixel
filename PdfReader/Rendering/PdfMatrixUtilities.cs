using PdfReader.Models;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Utility methods for matrix operations and operand handling
    /// Focused on mathematical operations for PDF rendering
    /// </summary>
    public static class PdfMatrixUtilities
    {
        /// <summary>
        /// Create an SKMatrix from PDF transformation matrix operands
        /// </summary>
        public static SKMatrix CreateMatrix(List<IPdfValue> operands)
        {
            if (operands == null || operands.Count < 6)
            {
                return SKMatrix.Identity;
            }

            var a = operands[0].AsFloat();  // scaleX
            var b = operands[1].AsFloat();  // skewY 
            var c = operands[2].AsFloat();  // skewX
            var d = operands[3].AsFloat();  // scaleY
            var e = operands[4].AsFloat();  // transX
            var f = operands[5].AsFloat();  // transY

            var result = new SKMatrix(
                a, c, e,     // scaleX, skewX, transX
                b, d, f,    // skewY, scaleY, transY
                0, 0, 1);

            return result;
        }
    }
}