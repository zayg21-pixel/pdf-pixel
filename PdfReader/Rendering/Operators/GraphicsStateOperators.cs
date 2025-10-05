using System;
using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    public static class GraphicsStateOperators
    {
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, ref PdfGraphicsState graphicsState, Stack<PdfGraphicsState> graphicsStack, SKCanvas canvas, PdfPage page)
        {
            switch (op)
            {
                case "q": // Save graphics state
                    ProcessSaveGraphicsState(graphicsState, graphicsStack, canvas);
                    return true;
                case "Q": // Restore graphics state
                    ProcessRestoreGraphicsState(ref graphicsState, graphicsStack, canvas);
                    return true;
                case "cm": // Concatenate matrix
                    ProcessConcatenateMatrix(operandStack, canvas, graphicsState);
                    return true;
                case "w": // Set line width
                    ProcessSetLineWidth(operandStack, graphicsState);
                    return true;
                case "J": // Set line cap style
                    ProcessSetLineCap(operandStack, graphicsState);
                    return true;
                case "j": // Set line join style
                    ProcessSetLineJoin(operandStack, graphicsState);
                    return true;
                case "M": // Set miter limit
                    ProcessSetMiterLimit(operandStack, graphicsState);
                    return true;
                case "d": // Set dash pattern
                    ProcessSetDashPattern(operandStack, graphicsState);
                    return true;
                case "gs": // Set graphics state parameters from ExtGState
                    ProcessSetGraphicsStateParameters(operandStack, graphicsState, canvas, page);
                    return true;
                case "ri": // Set rendering intent
                    ProcessSetRenderingIntent(operandStack, graphicsState);
                    return true;
                case "i": // Set flatness tolerance
                    ProcessSetFlatnessTolerance(operandStack);
                    return true;
                default:
                    return false;
            }
        }

        private static void ProcessSetRenderingIntent(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var riOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (riOperands.Count > 0)
            {
                var name = riOperands[0].AsName();
                graphicsState.RenderingIntent = PdfRenderingIntentUtilities.ParseRenderingIntent(name);
            }
        }

        private static void ProcessSaveGraphicsState(PdfGraphicsState graphicsState, Stack<PdfGraphicsState> graphicsStack, SKCanvas canvas)
        {
            graphicsStack.Push(graphicsState.Clone());
            canvas.Save();
        }

        private static void ProcessRestoreGraphicsState(ref PdfGraphicsState graphicsState, Stack<PdfGraphicsState> graphicsStack, SKCanvas canvas)
        {
            if (graphicsStack.Count > 0)
            {
                graphicsState = graphicsStack.Pop();
                canvas.Restore();
            }
        }

        private static void ProcessConcatenateMatrix(Stack<IPdfValue> operandStack, SKCanvas canvas, PdfGraphicsState graphicsState)
        {
            var matrixOperands = PdfOperatorProcessor.GetOperands(6, operandStack);
            if (matrixOperands.Count >= 6)
            {
                var matrix = PdfMatrixUtilities.CreateMatrix(matrixOperands);
                canvas.Concat(matrix);
                
                // Update CTM in graphics state - concatenate with existing CTM
                graphicsState.CTM = matrix.PostConcat(graphicsState.CTM);
            }
        }

        private static void ProcessSetLineWidth(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var widthOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (widthOperands.Count > 0)
                graphicsState.LineWidth = widthOperands[0].AsFloat();
        }

        private static void ProcessSetLineCap(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var capOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (capOperands.Count > 0)
            {
                var capStyle = capOperands[0].AsFloat();
                graphicsState.LineCap = capStyle switch
                {
                    0 => SKStrokeCap.Butt,
                    1 => SKStrokeCap.Round,
                    2 => SKStrokeCap.Square,
                    _ => SKStrokeCap.Butt
                };
            }
        }

        private static void ProcessSetLineJoin(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var joinOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (joinOperands.Count > 0)
            {
                var joinStyle = joinOperands[0].AsFloat();
                graphicsState.LineJoin = joinStyle switch
                {
                    0 => SKStrokeJoin.Miter,
                    1 => SKStrokeJoin.Round,
                    2 => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Miter
                };
            }
        }

        private static void ProcessSetMiterLimit(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var miterOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (miterOperands.Count > 0)
            {
                graphicsState.MiterLimit = miterOperands[0].AsFloat();
            }
        }

        private static void ProcessSetDashPattern(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var dashOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (dashOperands.Count >= 2)
            {
                var dashArray = dashOperands[0].AsArray().GetFloatArray();
                var dashPhase = dashOperands[1].AsFloat();

                if (dashArray != null && dashArray.Length > 0)
                {
                    graphicsState.DashPattern = dashArray;
                    graphicsState.DashPhase = dashPhase;
                }
                else
                {
                    // Empty array means solid line
                    graphicsState.DashPattern = null;
                    graphicsState.DashPhase = 0;
                }
            }
        }

        private static void ProcessSetGraphicsStateParameters(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            var gsOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (gsOperands.Count > 0)
            {
                var gsName = gsOperands[0].AsName();
                // Apply graphics state parameters from the page resources and get any transformation matrix
                var transformMatrix = PdfGraphicsStateParser.ParseGraphicsStateParameters(gsName, graphicsState, page);
                
                // Apply transformation matrix to canvas and update CTM if present
                if (transformMatrix.HasValue)
                {
                    canvas.Concat(transformMatrix.Value);
                    // Update CTM in graphics state - concatenate with existing CTM
                    graphicsState.CTM = transformMatrix.Value.PostConcat(graphicsState.CTM);
                }
            }
        }

        private static void ProcessSetFlatnessTolerance(Stack<IPdfValue> operandStack)
        {
            var iOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (iOperands.Count > 0)
            {
                var flatness = iOperands[0].AsFloat();
                Console.WriteLine($"Flatness tolerance: {flatness} (not implemented)");
            }
        }
    }
}