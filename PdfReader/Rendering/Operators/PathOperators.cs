using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles path construction and painting operators
    /// </summary>
    public static class PathOperators
    {
        /// <summary>
        /// Process path-related operators
        /// </summary>
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState,
                                         SKCanvas canvas, SKPath currentPath, PdfPage page)
        {
            switch (op)
            {
                // Path construction
                case "m": // Move to
                    ProcessMoveTo(operandStack, currentPath);
                    return true;

                case "l": // Line to
                    ProcessLineTo(operandStack, currentPath);
                    return true;

                case "c": // Curve to
                    ProcessCurveTo(operandStack, currentPath);
                    return true;

                case "v": // Curve to (first control point = current point)
                    ProcessCurveToV(operandStack, currentPath);
                    return true;

                case "y": // Curve to (second control point = end point)
                    ProcessCurveToY(operandStack, currentPath);
                    return true;

                case "h": // Close path
                    ProcessClosePath(currentPath);
                    return true;

                case "re": // Rectangle
                    ProcessRectangle(operandStack, currentPath);
                    return true;

                // Clipping operators
                case "W": // Set clipping path using non-zero winding rule
                    ProcessSetClippingPath(canvas, currentPath, SKPathFillType.Winding);
                    return true;

                case "W*": // Set clipping path using even-odd rule
                    ProcessSetClippingPath(canvas, currentPath, SKPathFillType.EvenOdd);
                    return true;

                // Path painting operators
                case "S": // Stroke
                    ProcessStrokePath(canvas, currentPath, graphicsState, page);
                    return true;

                case "s": // Close and stroke
                    ProcessCloseAndStrokePath(canvas, currentPath, graphicsState, page);
                    return true;

                case "f": // Fill using non-zero winding rule
                case "F": // Fill using non-zero winding rule (alternative)
                    ProcessFillPath(canvas, currentPath, graphicsState, SKPathFillType.Winding, page);
                    return true;

                case "f*": // Fill using even-odd rule
                    ProcessFillPath(canvas, currentPath, graphicsState, SKPathFillType.EvenOdd, page);
                    return true;

                case "B": // Fill and stroke using non-zero winding rule
                    ProcessFillAndStrokePath(canvas, currentPath, graphicsState, SKPathFillType.Winding, page);
                    return true;

                case "B*": // Fill and stroke using even-odd rule
                    ProcessFillAndStrokePath(canvas, currentPath, graphicsState, SKPathFillType.EvenOdd, page);
                    return true;

                case "b": // Close, fill and stroke using non-zero winding rule
                    ProcessCloseFillAndStrokePath(canvas, currentPath, graphicsState, SKPathFillType.Winding, page);
                    return true;

                case "b*": // Close, fill and stroke using even-odd rule
                    ProcessCloseFillAndStrokePath(canvas, currentPath, graphicsState, SKPathFillType.EvenOdd, page);
                    return true;

                case "n": // End path
                    ProcessEndPath(currentPath);
                    return true;

                default:
                    return false; // Not handled by this class
            }
        }

        private static void ProcessMoveTo(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var moveOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (moveOperands.Count >= 2)
            {
                var x = moveOperands[0].AsFloat();
                var y = moveOperands[1].AsFloat();
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.MoveTo(x, y);
            }
        }

        private static void ProcessLineTo(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var lineOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (lineOperands.Count >= 2)
            {
                var x = lineOperands[0].AsFloat();
                var y = lineOperands[1].AsFloat();
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.LineTo(x, y);
            }
        }

        private static void ProcessCurveTo(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var curveOperands = PdfOperatorProcessor.GetOperands(6, operandStack);
            if (curveOperands.Count >= 6)
            {
                var x1 = curveOperands[0].AsFloat();
                var y1 = curveOperands[1].AsFloat();
                var x2 = curveOperands[2].AsFloat();
                var y2 = curveOperands[3].AsFloat();
                var x3 = curveOperands[4].AsFloat();
                var y3 = curveOperands[5].AsFloat();
                
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.CubicTo(x1, y1, x2, y2, x3, y3);
            }
        }

        private static void ProcessCurveToV(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var vOperands = PdfOperatorProcessor.GetOperands(4, operandStack);
            if (vOperands.Count >= 4)
            {
                // Current point is used as first control point
                var lastPoint = currentPath.LastPoint;
                var x2 = vOperands[0].AsFloat();
                var y2 = vOperands[1].AsFloat();
                var x3 = vOperands[2].AsFloat();
                var y3 = vOperands[3].AsFloat();
                
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.CubicTo(
                    lastPoint.X, lastPoint.Y, // First control point = current point (already transformed)
                    x2, y2, // Second control point
                    x3, y3); // End point
            }
        }

        private static void ProcessCurveToY(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var yOperands = PdfOperatorProcessor.GetOperands(4, operandStack);
            if (yOperands.Count >= 4)
            {
                var x1 = yOperands[0].AsFloat();
                var y1 = yOperands[1].AsFloat();
                var x3 = yOperands[2].AsFloat();
                var y3 = yOperands[3].AsFloat();
                
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.CubicTo(
                    x1, y1, // First control point
                    x3, y3, // Second control point = end point
                    x3, y3); // End point
            }
        }

        private static void ProcessClosePath(SKPath currentPath)
        {
            currentPath.Close();
        }

        private static void ProcessRectangle(Stack<IPdfValue> operandStack, SKPath currentPath)
        {
            var rectOperands = PdfOperatorProcessor.GetOperands(4, operandStack);
            if (rectOperands.Count >= 4)
            {
                var x = rectOperands[0].AsFloat();
                var y = rectOperands[1].AsFloat();
                var width = rectOperands[2].AsFloat();
                var height = rectOperands[3].AsFloat();
                
                // Use coordinates directly - global transformation handles coordinate system conversion
                currentPath.AddRect(new SKRect(x, y, x + width, y + height));
            }
        }

        private static void ProcessSetClippingPath(SKCanvas canvas, SKPath currentPath, SKPathFillType fillType)
        {
            if (!currentPath.IsEmpty)
            {
                currentPath.FillType = fillType;
                canvas.ClipPath(currentPath, SKClipOperation.Intersect);
            }
        }

        private static void ProcessStrokePath(SKCanvas canvas, SKPath currentPath, PdfGraphicsState graphicsState, PdfPage page)
        {
            page.Document.PdfRenderer.PaintPath(canvas, currentPath, graphicsState, PaintOperation.Stroke, page);
            currentPath.Reset();
        }

        private static void ProcessCloseAndStrokePath(SKCanvas canvas, SKPath currentPath, PdfGraphicsState graphicsState, PdfPage page)
        {
            currentPath.Close();
            page.Document.PdfRenderer.PaintPath(canvas, currentPath, graphicsState, PaintOperation.Stroke, page);
            currentPath.Reset();
        }

        private static void ProcessFillPath(SKCanvas canvas, SKPath currentPath, PdfGraphicsState graphicsState, SKPathFillType fillType, PdfPage page)
        {
            page.Document.PdfRenderer.PaintPath(canvas, currentPath, graphicsState, PaintOperation.Fill, page, fillType);
            currentPath.Reset();
        }

        private static void ProcessFillAndStrokePath(SKCanvas canvas, SKPath currentPath, PdfGraphicsState graphicsState, SKPathFillType fillType, PdfPage page)
        {
            page.Document.PdfRenderer.PaintPath(canvas, currentPath, graphicsState, PaintOperation.FillAndStroke, page, fillType);
            currentPath.Reset();
        }

        private static void ProcessCloseFillAndStrokePath(SKCanvas canvas, SKPath currentPath, PdfGraphicsState graphicsState, SKPathFillType fillType, PdfPage page)
        {
            currentPath.Close();
            page.Document.PdfRenderer.PaintPath(canvas, currentPath, graphicsState, PaintOperation.FillAndStroke, page, fillType);
            currentPath.Reset();
        }

        private static void ProcessEndPath(SKPath currentPath)
        {
            currentPath.Reset();
        }
    }
}