using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Commands.Model;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles path construction, clipping, and painting operators.
/// Converted to an instance implementation that conforms to <see cref="IOperatorProcessor"/>.
/// </summary>
internal class PathOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        // Path construction
        "m",
        "l",
        "c",
        "v",
        "y",
        "h",
        "re",
        // Clipping
        "W",
        "W*",
        // Painting
        "S",
        "s",
        "f",
        "F",
        "f*",
        "B",
        "B*",
        "b",
        "b*",
        "n"
    ];

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfPathBuilder _currentPath;
    private readonly IPdfPageInternal _page;
    private readonly ILogger<PathOperators> _logger;
    private PdfPathFillType? _pendingClipFillType;
    private PdfPoint _lastPoint;
    private PdfPoint _subPathStart;

    public PathOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, IPdfCommandProcessor processor, PdfPathBuilder currentPath, IPdfPageInternal page)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _processor = processor;
        _currentPath = currentPath;
        _page = page;
        _logger = page.Document.LoggerFactory.CreateLogger<PathOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
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
                ProcessSetClippingPath(PdfPathFillType.Winding);
                break;
            }
            case "W*":
            {
                ProcessSetClippingPath(PdfPathFillType.EvenOdd);
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
                ProcessFillPath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case "f*":
            {
                ProcessFillPath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case "B":
            {
                ProcessFillAndStrokePath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case "B*":
            {
                ProcessFillAndStrokePath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case "b":
            {
                ProcessCloseFillAndStrokePath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case "b*":
            {
                ProcessCloseFillAndStrokePath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case "n":
            {
                ProcessEndPath(graphicsState);
                break;
            }
        }
    }

    // ---------------------- Helper Implementations --------------------------
    private void ProcessMoveTo()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        float? x = operands[0].AsFloat();
        float? y = operands[1].AsFloat();

        if (x == null || y == null)
        {
            _logger.LogWarning("Skipping 'm' operator: non-numeric coordinate operands.");
            return;
        }

        _currentPath.MoveTo(x.Value, y.Value);
        _lastPoint = new PdfPoint(x.Value, y.Value);
        _subPathStart = _lastPoint;
    }

    private void ProcessLineTo()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        float? x = operands[0].AsFloat();
        float? y = operands[1].AsFloat();

        if (x == null || y == null)
        {
            _logger.LogWarning("Skipping 'l' operator: non-numeric coordinate operands.");
            return;
        }

        _currentPath.LineTo(x.Value, y.Value);
        _lastPoint = new PdfPoint(x.Value, y.Value);
    }

    private void ProcessCurveTo()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
        if (operands.Count < 6)
        {
            return;
        }

        float? x1 = operands[0].AsFloat();
        float? y1 = operands[1].AsFloat();
        float? x2 = operands[2].AsFloat();
        float? y2 = operands[3].AsFloat();
        float? x3 = operands[4].AsFloat();
        float? y3 = operands[5].AsFloat();

        if (x1 == null
            || y1 == null
            || x2 == null
            || y2 == null
            || x3 == null
            || y3 == null)
        {
            _logger.LogWarning("Skipping 'c' operator: non-numeric coordinate operands.");
            return;
        }

        _currentPath.CubicTo(x1.Value, y1.Value, x2.Value, y2.Value, x3.Value, y3.Value);
        _lastPoint = new PdfPoint(x3.Value, y3.Value);
    }

    private void ProcessCurveToV()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
        if (operands.Count < 4)
        {
            return;
        }

        float? x2 = operands[0].AsFloat();
        float? y2 = operands[1].AsFloat();
        float? x3 = operands[2].AsFloat();
        float? y3 = operands[3].AsFloat();

        if (x2 == null || y2 == null || x3 == null || y3 == null)
        {
            _logger.LogWarning("Skipping 'v' operator: non-numeric coordinate operands.");
            return;
        }

        _currentPath.CubicTo(_lastPoint.X, _lastPoint.Y, x2.Value, y2.Value, x3.Value, y3.Value);
        _lastPoint = new PdfPoint(x3.Value, y3.Value);
    }

    private void ProcessCurveToY()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
        if (operands.Count < 4)
        {
            return;
        }

        float? x1 = operands[0].AsFloat();
        float? y1 = operands[1].AsFloat();
        float? x3 = operands[2].AsFloat();
        float? y3 = operands[3].AsFloat();

        if (x1 == null || y1 == null || x3 == null || y3 == null)
        {
            _logger.LogWarning("Skipping 'y' operator: non-numeric coordinate operands.");
            return;
        }

        _currentPath.CubicTo(x1.Value, y1.Value, x3.Value, y3.Value, x3.Value, y3.Value);
        _lastPoint = new PdfPoint(x3.Value, y3.Value);
    }

    private void ProcessClosePath()
    {
        _currentPath.Close();
        _lastPoint = _subPathStart;
    }

    private void ProcessRectangle()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(4, _operandStack);
        if (operands.Count < 4)
        {
            return;
        }

        float? x = operands[0].AsFloat();
        float? y = operands[1].AsFloat();
        float? width = operands[2].AsFloat();
        float? height = operands[3].AsFloat();

        if (x == null || y == null || width == null || height == null)
        {
            _logger.LogWarning("Skipping 're' operator: non-numeric rectangle operands.");
            return;
        }

        _currentPath.AddRect(new PdfRectangle(x.Value, y.Value, x.Value + width.Value, y.Value + height.Value));
        _lastPoint = new PdfPoint(x.Value, y.Value);
        _subPathStart = _lastPoint;
    }

    private void ProcessSetClippingPath(PdfPathFillType fillType) => _pendingClipFillType = fillType;

    private void ApplyPendingClip(PdfGraphicsState graphicsState)
    {
        if (_pendingClipFillType == null)
        {
            return;
        }

        if (!_currentPath.IsEmpty)
        {
            PdfPath clipPath = _currentPath.ToPath(_pendingClipFillType.Value);
            _processor.Process(new ClipPathCommand(clipPath, PdfClipOperation.Intersect));
            graphicsState.IntersectClipBounds(clipPath.GetBounds());
        }

        _pendingClipFillType = null;
    }

    private void ProcessStrokePath(PdfGraphicsState graphicsState)
    {
        ApplyPendingClip(graphicsState);
        _renderer.DrawPath(_processor, _currentPath.ToPath(PdfPathFillType.Winding), graphicsState, PdfPaintOperation.Stroke);
        _currentPath.Reset();
    }

    private void ProcessCloseAndStrokePath(PdfGraphicsState graphicsState)
    {
        _currentPath.Close();
        ApplyPendingClip(graphicsState);
        _renderer.DrawPath(_processor, _currentPath.ToPath(PdfPathFillType.Winding), graphicsState, PdfPaintOperation.Stroke);
        _currentPath.Reset();
    }

    private void ProcessFillPath(PdfGraphicsState graphicsState, PdfPathFillType fillType)
    {
        ApplyPendingClip(graphicsState);
        _renderer.DrawPath(_processor, _currentPath.ToPath(fillType), graphicsState, PdfPaintOperation.Fill);
        _currentPath.Reset();
    }

    private void ProcessFillAndStrokePath(PdfGraphicsState graphicsState, PdfPathFillType fillType)
    {
        ApplyPendingClip(graphicsState);
        _renderer.DrawPath(_processor, _currentPath.ToPath(fillType), graphicsState, PdfPaintOperation.FillAndStroke);
        _currentPath.Reset();
    }

    private void ProcessCloseFillAndStrokePath(PdfGraphicsState graphicsState, PdfPathFillType fillType)
    {
        _currentPath.Close();
        ApplyPendingClip(graphicsState);
        _renderer.DrawPath(_processor, _currentPath.ToPath(fillType), graphicsState, PdfPaintOperation.FillAndStroke);
        _currentPath.Reset();
    }

    private void ProcessEndPath(PdfGraphicsState graphicsState)
    {
        ApplyPendingClip(graphicsState);
        _currentPath.Reset();
    }
}
