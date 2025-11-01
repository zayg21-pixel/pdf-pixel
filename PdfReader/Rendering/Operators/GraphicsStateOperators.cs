using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.State;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    public class GraphicsStateOperators : IOperatorProcessor
    {
        private static readonly HashSet<string> SupportedOperators = new HashSet<string>
        {
            "q","Q","cm","w","J","j","M","d","gs","ri","i"
        };

        private readonly PdfPage _page;
        private readonly SKCanvas _canvas;
        private readonly Stack<IPdfValue> _operandStack;
        private readonly Stack<PdfGraphicsState> _graphicsStack;
        private readonly ILogger<GraphicsStateOperators> _logger;

        public GraphicsStateOperators(PdfPage page, SKCanvas canvas, Stack<IPdfValue> operandStack, Stack<PdfGraphicsState> graphicsStack)
        {
            _page = page;
            _canvas = canvas;
            _operandStack = operandStack;
            _graphicsStack = graphicsStack;
            _logger = page.Document.LoggerFactory.CreateLogger<GraphicsStateOperators>();
        }

        public bool CanProcess(string op)
        {
            return SupportedOperators.Contains(op);
        }

        public void ProcessOperator(string op, ref PdfParseContext parseContext, ref PdfGraphicsState graphicsState)
        {
            switch (op)
            {
                case "q":
                {
                    // Save graphics state
                    ProcessSaveGraphicsState(graphicsState);
                    break;
                }
                case "Q":
                {
                    // Restore graphics state
                    ProcessRestoreGraphicsState(ref graphicsState);
                    break;
                }
                case "cm":
                {
                    // Concatenate matrix
                    ProcessConcatenateMatrix(graphicsState);
                    break;
                }
                case "w":
                {
                    // Set line width
                    ProcessSetLineWidth(graphicsState);
                    break;
                }
                case "J":
                {
                    // Set line cap style
                    ProcessSetLineCap(graphicsState);
                    break;
                }
                case "j":
                {
                    // Set line join style
                    ProcessSetLineJoin(graphicsState);
                    break;
                }
                case "M":
                {
                    // Set miter limit
                    ProcessSetMiterLimit(graphicsState);
                    break;
                }
                case "d":
                {
                    // Set dash pattern
                    ProcessSetDashPattern(graphicsState);
                    break;
                }
                case "gs":
                {
                    // Set graphics state parameters from ExtGState
                    ProcessSetGraphicsStateParameters(graphicsState);
                    break;
                }
                case "ri":
                {
                    // Set rendering intent
                    ProcessSetRenderingIntent(graphicsState);
                    break;
                }
                case "i":
                {
                    // Set flatness tolerance
                    ProcessSetFlatnessTolerance(graphicsState);
                    break;
                }
            }
        }

        private void ProcessSetRenderingIntent(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            graphicsState.RenderingIntent = operands[0].AsName().AsEnum<PdfRenderingIntent>();
        }

        private void ProcessSaveGraphicsState(PdfGraphicsState graphicsState)
        {
            _graphicsStack.Push(graphicsState.Clone());
            _canvas.Save();
        }

        private void ProcessRestoreGraphicsState(ref PdfGraphicsState graphicsState)
        {
            if (_graphicsStack.Count == 0)
            {
                return;
            }

            graphicsState = _graphicsStack.Pop();
            _canvas.Restore();
        }

        private void ProcessConcatenateMatrix(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
            if (operands.Count < 6)
            {
                return;
            }

            var matrix = PdfMatrixUtilities.CreateMatrix(operands);
            _canvas.Concat(matrix);

            // Update CTM in graphics state - concatenate with existing CTM
            graphicsState.CTM = matrix.PostConcat(graphicsState.CTM);
        }

        private void ProcessSetLineWidth(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            graphicsState.LineWidth = operands[0].AsFloat();
        }

        private void ProcessSetLineCap(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var capStyle = operands[0].AsFloat();
            graphicsState.LineCap = capStyle switch
            {
                0 => SKStrokeCap.Butt,
                1 => SKStrokeCap.Round,
                2 => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt
            };
        }

        private void ProcessSetLineJoin(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var joinStyle = operands[0].AsFloat();
            graphicsState.LineJoin = joinStyle switch
            {
                0 => SKStrokeJoin.Miter,
                1 => SKStrokeJoin.Round,
                2 => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter
            };
        }

        private void ProcessSetMiterLimit(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            graphicsState.MiterLimit = operands[0].AsFloat();
        }

        private void ProcessSetDashPattern(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
            if (operands.Count < 2)
            {
                return;
            }

            var dashArray = operands[0].AsArray().GetFloatArray();
            var dashPhase = operands[1].AsFloat();

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

        private void ProcessSetGraphicsStateParameters(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var gsName = operands[0].AsName();
            // Apply graphics state parameters from the page resources and get any transformation matrix
            var transformMatrix = PdfGraphicsStateParser.ParseGraphicsStateParameters(gsName, graphicsState, _page);

            if (!transformMatrix.HasValue)
            {
                return;
            }

            _canvas.Concat(transformMatrix.Value);
            // Update CTM in graphics state - concatenate with existing CTM
            graphicsState.CTM = transformMatrix.Value.PostConcat(graphicsState.CTM);
        }

        private void ProcessSetFlatnessTolerance(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var flatness = operands[0].AsFloat();
            graphicsState.FlatnessTolerance = flatness;
        }
    }
}