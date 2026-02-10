using PdfPixel.Functions;
using PdfPixel.Models;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform
{
    /// <summary>
    /// Implements <see cref="IColorTransform"/> for PDF transfer functions (TR).
    /// Supports a single function applied to all RGB components or an array of per-component functions.
    /// The input and output component ranges are expected to be [0,1].
    /// </summary>
    public sealed class TransferFunctionTransform : IColorTransform
    {
        private readonly PdfFunction _fx;
        private readonly PdfFunction _fy;
        private readonly PdfFunction _fz;

        private TransferFunctionTransform(PdfFunction fx, PdfFunction fy, PdfFunction fz)
        {
            _fx = fx;
            _fy = fy;
            _fz = fz;
        }

        public bool IsIdentity => false;

        /// <summary>
        /// Creates a <see cref="TransferFunctionTransform"/> from a PDF function object or an array of function objects.
        /// Accepts only the following non-spec compliant forms:
        /// - A single function: applied to X, Y, Z components.
        /// - An array with at least 3 functions: first three applied to X, Y, Z components.
        /// Returns null if the input is not compliant or parsing fails.
        /// </summary>
        /// <param name="functionObject">The PDF object representing TR function(s).</param>
        /// <returns>A <see cref="TransferFunctionTransform"/> instance, or null for non-compliant input.</returns>
        public static TransferFunctionTransform FromPdfObject(PdfObject functionObject)
        {
            if (functionObject == null || functionObject.Value == null)
            {
                return null;
            }

            var value = functionObject.Value;

            if (value.Type == PdfValueType.Array)
            {
                var arr = value.AsArray();
                if (arr == null)
                {
                    return null;
                }

                // Non-spec compliant rule: accept arrays with at least 3 functions.
                if (arr.Count < 3)
                {
                    return null;
                }

                PdfFunction fx = PdfFunctions.GetFunction(arr.GetObject(0));
                PdfFunction fy = PdfFunctions.GetFunction(arr.GetObject(1));
                PdfFunction fz = PdfFunctions.GetFunction(arr.GetObject(2));

                if (fx == null || fy == null || fz == null)
                {
                    return null;
                }

                return new TransferFunctionTransform(fx, fy, fz);
            }
            else
            {
                // Non-spec compliant rule: single function applied to all components.
                var fn = PdfFunctions.GetFunction(functionObject);
                if (fn == null)
                {
                    return null;
                }
                return new TransferFunctionTransform(fn, fn, fn);
            }
        }

        /// <summary>
        /// Applies transfer functions per RGB component. Preserves the W component.
        /// </summary>
        /// <param name="color">Input color vector with X, Y, Z used as device components.</param>
        /// <returns>Transformed color vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Transform(Vector4 color)
        {
            float x = color.X;
            float y = color.Y;
            float z = color.Z;

            var rx = _fx.Evaluate(x);
            if (!rx.IsEmpty)
            {
                x = rx[0];
            }

            var ry = _fy.Evaluate(y);
            if (!ry.IsEmpty)
            {
                y = ry[0];
            }

            var rz = _fz.Evaluate(z);
            if (!rz.IsEmpty)
            {
                z = rz[0];
            }

            return new Vector4(x, y, z, color.W);
        }
    }
}
