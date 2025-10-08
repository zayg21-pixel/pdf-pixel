using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Parsing;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles path construction, clipping, and painting operators.
    /// Converted to an instance implementation that conforms to <see cref="IOperatorProcessor"/>.
    /// </summary>
    public class PathOperators : IOperatorProcessor
    {
        private static readonly HashSet<string> SupportedOperators = new HashSet<string>
        {
            // Path construction
            "m","l","c","v","y","h","re",
            // Clipping
            "W","W*",
            // Painting
            "S","s","f","F","f*","B","B*","b","b*","n"
        };

        private readonly Stack<IPdfValue> _operandStack;
        private readonly SKCanvas _canvas;
        private readonly SKPath _currentPath;
        private readonly PdfPage _page;

        public PathOperators(Stack<IPdfValue> operandStack, SKCanvas canvas, SKPath currentPath, PdfPage page)
        {
            _operandStack = operandStack;
            _canvas = canvas;
            _currentPath = currentPath;
            _page = page;
        }

        public bool CanProcess(string op)
        {
            return SupportedOperators.Contains(op);
        }

        public void ProcessOperator(string op, ref PdfParseContext parseContext, ref PdfGraphicsState graphicsState)
        {
            switch (op)
            {
                // -----------------------------------------------------------------
                // Path construction operators
                // -----------------------------------------------------------------
                case "m":
                {
                    ProcessMoveTo();
                    break;
                }
                case "l":
                {
                    ProcessLineTo();
                    break;
                }
                case "c":
                {
                    ProcessCurveTo();
                    break;
                }
                case "v":
                {
                    ProcessCurveToV();
                    break;
                }
                case "y":
                {
                    ProcessCurveToY();
                    break;
                }
                case "h":
                {
                    ProcessClosePath();
                    break;
                }
                case "re":
                {
                    ProcessRectangle();
                    break;
                }

                // -----------------------------------------------------------------
                // Clipping path operators (establish clipping path from current path)
                // -----------------------------------------------------------------
                case "W":
                {
                    ProcessSetClippingPath(SKPathFillType.Winding);
                    break;
                }
                case "W*":
                {
                    ProcessSetClippingPath(SKPathFillType.EvenOdd);
                    break;
                }

                // -----------------------------------------------------------------
                // Path painting operators
                // -----------------------------------------------------------------
                case "S":
                {
                    ProcessStrokePath(graphicsState);
                    break;
                }
                case "s":
                {
                    ProcessCloseAndStrokePath(graphicsState);
                    break;
                }
                case "f":
                case "F":
                {
                    ProcessFillPath(graphicsState, SKPathFillType.Winding);
                    break;
                }
                case "f*":
                {
                    ProcessFillPath(graphicsState, SKPathFillType.EvenOdd);
                    break;
                }
                case "B":
                {
                    ProcessFillAndStrokePath(graphicsState, SKPathFillType.Winding);
                    break;
                }
                case "B*":
                {
                    ProcessFillAndStrokePath(graphicsState, SKPathFillType.EvenOdd);
                    break;
                }
                case "b":
                {
                    ProcessCloseFillAndStrokePath(graphicsState, SKPathFillType.Winding);
                    break;
                }
                case "b*":
                {
                    ProcessCloseFillAndStrokePath(graphicsState, SKPathFillType.EvenOdd);
                    break;
                }
                case "n":
                {
                    ProcessEndPath();
                    break;
                }
            }
        }

        // ---------------------- Helper Implementations --------------------------
        private void ProcessMoveTo()
        {
            var operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
            if (operands.Count < 2)
            {
                return;
            }
            var x = operands[0].AsFloat();
            var y = operands[1].AsFloat();
            _currentPath.MoveTo(x, y);
        }

        private void ProcessLineTo()
        {
            var operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
            if (operands.Count < 2)
            {
                return;
            }
            var x = operands[0].AsFloat();
            var y = operands[1].AsFloat();
            _currentPath.LineTo(x, y);
        }

        private void ProcessCurveTo()
        {
            var operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
            if (operands.Count < 6)
            {
                return;
            }
            var x1 = operands[0].AsFloat();
            var y1 = operands[1].AsFloat();
            var x2 = operands[2].AsFloat();
            var y2 = operands[3].AsFloat();
            var x3 = operands[4].AsFloat();
            var y3 = operands[5].AsFloat();
            _currentPath.CubicTo(x1, y1, x2, y2, x3, y3);
        }

        private void ProcessCurveToV()
        {
            var operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
            if (operands.Count < 4)
            {
                return;
            }
            var lastPoint = _currentPath.LastPoint;
            var x2 = operands[0].AsFloat();
            var y2 = operands[1].AsFloat();
            var x3 = operands[2].AsFloat();
            var y3 = operands[3].AsFloat();
            _currentPath.CubicTo(lastPoint.X, lastPoint.Y, x2, y2, x3, y3);
        }

        private void ProcessCurveToY()
        {
            var operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
            if (operands.Count < 4)
            {
                return;
            }
            var x1 = operands[0].AsFloat();
            var y1 = operands[1].AsFloat();
            var x3 = operands[2].AsFloat();
            var y3 = operands[3].AsFloat();
            _currentPath.CubicTo(x1, y1, x3, y3, x3, y3);
        }

        private void ProcessClosePath()
        {
            _currentPath.Close();
        }

        private void ProcessRectangle()
        {
            var operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
            if (operands.Count < 4)
            {
                return;
            }
            var x = operands[0].AsFloat();
            var y = operands[1].AsFloat();
            var width = operands[2].AsFloat();
            var height = operands[3].AsFloat();
            _currentPath.AddRect(new SKRect(x, y, x + width, y + height));
        }

        private void ProcessSetClippingPath(SKPathFillType fillType)
        {
            if (_currentPath.IsEmpty)
            {
                return;
            }
            _currentPath.FillType = fillType;
            _canvas.ClipPath(_currentPath, SKClipOperation.Intersect);
        }

        private void ProcessStrokePath(PdfGraphicsState graphicsState)
        {
            _page.Document.PdfRenderer.PaintPath(_canvas, _currentPath, graphicsState, PaintOperation.Stroke, _page);
            _currentPath.Reset();
        }

        private void ProcessCloseAndStrokePath(PdfGraphicsState graphicsState)
        {
            _currentPath.Close();
            _page.Document.PdfRenderer.PaintPath(_canvas, _currentPath, graphicsState, PaintOperation.Stroke, _page);
            _currentPath.Reset();
        }

        private void ProcessFillPath(PdfGraphicsState graphicsState, SKPathFillType fillType)
        {
            _page.Document.PdfRenderer.PaintPath(_canvas, _currentPath, graphicsState, PaintOperation.Fill, _page, fillType);
            _currentPath.Reset();
        }

        private void ProcessFillAndStrokePath(PdfGraphicsState graphicsState, SKPathFillType fillType)
        {
            _page.Document.PdfRenderer.PaintPath(_canvas, _currentPath, graphicsState, PaintOperation.FillAndStroke, _page, fillType);
            _currentPath.Reset();
        }

        private void ProcessCloseFillAndStrokePath(PdfGraphicsState graphicsState, SKPathFillType fillType)
        {
            _currentPath.Close();
            _page.Document.PdfRenderer.PaintPath(_canvas, _currentPath, graphicsState, PaintOperation.FillAndStroke, _page, fillType);
            _currentPath.Reset();
        }

        private void ProcessEndPath()
        {
            _currentPath.Reset();
        }
    }
}