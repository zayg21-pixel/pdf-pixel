using Microsoft.Extensions.Logging;
using System;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Parsing;
using PdfPixel.Commands;
using PdfPixel.Objects;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles path construction, clipping, and painting operators.
/// Converted to an instance implementation that conforms to <see cref="IOperatorProcessor"/>.
/// </summary>
internal class PathOperators : IOperatorProcessor
{
    private readonly PdfRenderer _renderer;
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfPathBuilder _currentPath;
    private readonly IPdfContentHost _host;
    private readonly ILogger<PathOperators> _logger;
    private PdfPathFillType? _pendingClipFillType;
    private PdfPoint _lastPoint;
    private PdfPoint _subPathStart;

    public PathOperators(PdfRenderer renderer, PdfOperandStack operandStack, IPdfCommandProcessor processor, PdfPathBuilder currentPath, IPdfContentHost host)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _processor = processor;
        _currentPath = currentPath;
        _host = host;
        _logger = host.Document.LoggerFactory.CreateLogger<PathOperators>();
    }

    public bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            // -----------------------------------------------------------------
            // Path construction operators
            // -----------------------------------------------------------------
            case PdfOperator.MoveTo:
            {
                ProcessMoveTo();
                break;
            }
            case PdfOperator.LineTo:
            {
                ProcessLineTo();
                break;
            }
            case PdfOperator.CurveTo:
            {
                ProcessCurveTo();
                break;
            }
            case PdfOperator.CurveToV:
            {
                ProcessCurveToV();
                break;
            }
            case PdfOperator.CurveToY:
            {
                ProcessCurveToY();
                break;
            }
            case PdfOperator.ClosePath:
            {
                ProcessClosePath();
                break;
            }
            case PdfOperator.Rectangle:
            {
                ProcessRectangle();
                break;
            }
            // -----------------------------------------------------------------
            // Clipping path operators (establish clipping path from current path)
            // -----------------------------------------------------------------
            case PdfOperator.Clip:
            {
                ProcessSetClippingPath(PdfPathFillType.Winding);
                break;
            }
            case PdfOperator.ClipEvenOdd:
            {
                ProcessSetClippingPath(PdfPathFillType.EvenOdd);
                break;
            }
            // -----------------------------------------------------------------
            // Path painting operators
            // -----------------------------------------------------------------
            case PdfOperator.Stroke:
            {
                ProcessStrokePath(graphicsState);
                break;
            }
            case PdfOperator.CloseAndStroke:
            {
                ProcessCloseAndStrokePath(graphicsState);
                break;
            }
            case PdfOperator.Fill:
            case PdfOperator.FillObsolete:
            {
                ProcessFillPath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case PdfOperator.FillEvenOdd:
            {
                ProcessFillPath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case PdfOperator.FillAndStroke:
            {
                ProcessFillAndStrokePath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case PdfOperator.FillAndStrokeEvenOdd:
            {
                ProcessFillAndStrokePath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case PdfOperator.CloseFillAndStroke:
            {
                ProcessCloseFillAndStrokePath(graphicsState, PdfPathFillType.Winding);
                break;
            }
            case PdfOperator.CloseFillAndStrokeEvenOdd:
            {
                ProcessCloseFillAndStrokePath(graphicsState, PdfPathFillType.EvenOdd);
                break;
            }
            case PdfOperator.EndPath:
            {
                ProcessEndPath(graphicsState);
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    // ---------------------- Helper Implementations --------------------------
    private void ProcessMoveTo()
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
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
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
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
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(6);
        if (operands.Length < 6)
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
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(4);
        if (operands.Length < 4)
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
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(4);
        if (operands.Length < 4)
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
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(4);
        if (operands.Length < 4)
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
