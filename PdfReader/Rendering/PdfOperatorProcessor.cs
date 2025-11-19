using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Rendering.Operators;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering;

/// <summary>
/// Handles PDF operator processing and execution for content streams
/// Delegates to specialized operator classes for better organization
/// </summary>
public class PdfOperatorProcessor
{
    private readonly IPdfRenderer _renderer;
    private readonly PdfPage _page;
    private readonly SKCanvas _canvas;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly Stack<PdfGraphicsState> _graphicsStack;
    private readonly SKPath _currentPath;
    private readonly GraphicsStateOperators _graphicsStateOperators;
    private readonly TextOperators _textOperators;
    private readonly PathOperators _pathOperators;
    private readonly ColorOperators _colorOperators;
    private readonly InlineImageOperators _inlineImageOperators;
    private readonly MiscellaneousOperators _miscOperators;
    private readonly HashSet<uint> _processingXObjects;
    private readonly ILogger<PdfOperatorProcessor> _logger;

    public PdfOperatorProcessor(IPdfRenderer renderer, PdfPage page, SKCanvas canvas, Stack<IPdfValue> operandStack, Stack<PdfGraphicsState> graphicsStack, SKPath currentPath, HashSet<uint> processingXObjects)
    {
        _renderer = renderer;
        _page = page;
        _canvas = canvas;
        _operandStack = operandStack;
        _graphicsStack = graphicsStack;
        _currentPath = currentPath;
        _processingXObjects = processingXObjects;
        _graphicsStateOperators = new GraphicsStateOperators(page, canvas, operandStack, graphicsStack);
        _textOperators = new TextOperators(renderer, page, canvas, operandStack);
        _pathOperators = new PathOperators(renderer, operandStack, canvas, currentPath, page);
        _colorOperators = new ColorOperators(renderer, operandStack, page);
        _inlineImageOperators = new InlineImageOperators(renderer, operandStack, page, canvas);
        _miscOperators = new MiscellaneousOperators(renderer, operandStack, page, canvas, processingXObjects);
        _logger = page.Document.LoggerFactory.CreateLogger<PdfOperatorProcessor>();
    }

    internal static List<IPdfValue> GetOperands(int count, Stack<IPdfValue> operandStack)
    {
        var operands = new List<IPdfValue>(count);
        for (int index = 0; index < count && operandStack.Count > 0; index++)
        {
            operands.Insert(0, operandStack.Pop());
        }
        return operands;
    }

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        bool handled = false;

        if (!handled && _graphicsStateOperators.CanProcess(op))
        {
            _graphicsStateOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled && _textOperators.CanProcess(op))
        {
            _textOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled && _pathOperators.CanProcess(op))
        {
            _pathOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled && _colorOperators.CanProcess(op))
        {
            _colorOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled && _inlineImageOperators.CanProcess(op))
        {
            _inlineImageOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled && _miscOperators.CanProcess(op))
        {
            _miscOperators.ProcessOperator(op, ref graphicsState);
            handled = true;
        }

        if (!handled)
        {
            ProcessUnknownOperator(op);
        }
    }

    private void ProcessUnknownOperator(string op)
    {
        _logger.LogWarning($"Unknown PDF operator '{op}' with {_operandStack.Count} operands on stack");
    }
}