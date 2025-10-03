using System;
using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles miscellaneous PDF operators that don't fit into other categories
    /// </summary>
    public static class MiscellaneousOperators
    {
        /// <summary>
        /// Process miscellaneous operators with XObject recursion tracking
        /// </summary>
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState,
                                         SKCanvas canvas, PdfPage page, HashSet<int> processingXObjects)
        {
            switch (op)
            {
                // XObject operators
                case "Do": // Invoke XObject
                    ProcessInvokeXObject(operandStack, graphicsState, canvas, page, processingXObjects);
                    return true;

                // Marked content operators
                case "MP": // Designate marked content point
                    ProcessMarkContentPoint(operandStack);
                    return true;

                case "DP": // Designate marked content point with property list
                    ProcessMarkContentPointWithProperties(operandStack);
                    return true;

                case "BMC": // Begin marked content sequence
                    ProcessBeginMarkedContent(operandStack);
                    return true;

                case "BDC": // Begin marked content sequence with property list
                    ProcessBeginMarkedContentWithProperties(operandStack);
                    return true;

                case "EMC": // End marked content sequence
                    ProcessEndMarkedContent();
                    return true;

                // Compatibility operators
                case "BX": // Begin compatibility section
                    ProcessBeginCompatibility();
                    return true;

                case "EX": // End compatibility section
                    ProcessEndCompatibility();
                    return true;

                // Type 3 font operators
                case "d0": // Set glyph width for Type 3 fonts
                    ProcessSetGlyphWidth(operandStack);
                    return true;

                case "d1": // Set glyph width and bounding box for Type 3 fonts
                    ProcessSetGlyphWidthAndBoundingBox(operandStack);
                    return true;

                // Shading operators
                case "sh": // Paint area defined by shading pattern
                    ProcessShading(operandStack, graphicsState, canvas, page);
                    return true;

                default:
                    return false; // Not handled by this class
            }
        }

        /// <summary>
        /// Process XObject invocation using dedicated XObject processor
        /// </summary>
        private static void ProcessInvokeXObject(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState,
                                               SKCanvas canvas, PdfPage page, HashSet<int> processingXObjects)
        {
            var xobjOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (xobjOperands.Count == 0)
            {
                return;
            }

            var xObjectName = xobjOperands[0].AsName();
            if (string.IsNullOrEmpty(xObjectName))
            {
                return;
            }

            // Delegate to dedicated XObject processor with recursion tracking
            PdfXObjectProcessor.ProcessXObject(xObjectName, graphicsState, canvas, page, processingXObjects);
        }

        // Marked content operators (safe to ignore for rendering)
        private static void ProcessMarkContentPoint(Stack<IPdfValue> operandStack)
        {
            var mpOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            // Safe to ignore - does not affect rendering
        }

        private static void ProcessMarkContentPointWithProperties(Stack<IPdfValue> operandStack)
        {
            var dpOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            // Safe to ignore - does not affect rendering
        }

        private static void ProcessBeginMarkedContent(Stack<IPdfValue> operandStack)
        {
            var bmcOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            // Safe to ignore - does not affect rendering
        }

        private static void ProcessBeginMarkedContentWithProperties(Stack<IPdfValue> operandStack)
        {
            var bdcOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            // Safe to ignore - does not affect rendering
        }

        private static void ProcessEndMarkedContent()
        {
            // Safe to ignore - does not affect rendering
        }

        // Compatibility operators (safe to ignore)
        private static void ProcessBeginCompatibility()
        {
            // Safe to ignore - does not affect rendering
        }

        private static void ProcessEndCompatibility()
        {
            // Safe to ignore - does not affect rendering
        }

        // Type 3 font operators (specialized font handling)
        private static void ProcessSetGlyphWidth(Stack<IPdfValue> operandStack)
        {
            var d0Operands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (d0Operands.Count >= 2)
            {
                var wx = d0Operands[0].AsFloat();
                var wy = d0Operands[1].AsFloat();
                Console.WriteLine($"Type 3 glyph width: {wx}, {wy}");
            }
        }

        private static void ProcessSetGlyphWidthAndBoundingBox(Stack<IPdfValue> operandStack)
        {
            var d1Operands = PdfOperatorProcessor.GetOperands(6, operandStack);
            if (d1Operands.Count >= 6)
            {
                var wx = d1Operands[0].AsFloat();
                var wy = d1Operands[1].AsFloat();
                var llx = d1Operands[2].AsFloat();
                var lly = d1Operands[3].AsFloat();
                var urx = d1Operands[4].AsFloat();
                var ury = d1Operands[5].AsFloat();
                Console.WriteLine($"Type 3 glyph width and bbox: w=({wx},{wy}), bbox=({llx},{lly},{urx},{ury})");
            }
        }

        // Shading operators (minimal support for type 2 and 3)
        private static void ProcessShading(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            var shadingOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (shadingOperands.Count == 0)
                return;

            var shadingName = shadingOperands[0].AsName();
            if (string.IsNullOrEmpty(shadingName))
                return;

            // Lookup shading dictionary from resources
            var shadings = page.ResourceDictionary.GetDictionary(PdfTokens.ShadingKey);
            var shadingDict = shadings?.GetDictionary(shadingName);
            if (shadingDict == null)
            {
                Console.WriteLine($"Shading '{shadingName}' not found in resources");
                return;
            }

            page.Document.PdfRenderer.ShadingDrawer.DrawShading(canvas, shadingDict, graphicsState, page);
        }
    }
}