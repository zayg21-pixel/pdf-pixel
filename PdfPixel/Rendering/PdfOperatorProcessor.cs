using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering;

/// <summary>
/// Handles PDF operator processing and execution for content streams
/// Delegates to specialized operator classes for better organization
/// </summary>
internal class PdfOperatorProcessor
{
    private readonly IPdfRenderer _renderer;
    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly Stack<PdfGraphicsState> _graphicsStack;
    private readonly PdfPathBuilder _currentPath;
    private readonly GraphicsStateOperators _graphicsStateOperators;
    private readonly TextOperators _textOperators;
    private readonly PathOperators _pathOperators;
    private readonly ColorOperators _colorOperators;
    private readonly InlineImageOperators _inlineImageOperators;
    private readonly MarkedContentOperators _markedContentOperators;
    private readonly MiscellaneousOperators _miscOperators;
    private readonly ILogger<PdfOperatorProcessor> _logger;

    public PdfOperatorProcessor(
        IPdfRenderer renderer,
        IPdfPageInternal page,
        IPdfCommandProcessor processor,
        Stack<IPdfValue> operandStack,
        Stack<PdfGraphicsState> graphicsStack,
        PdfPathBuilder currentPath)
    {
        _renderer = renderer;
        _page = page;
        _processor = processor;
        _operandStack = operandStack;
        _graphicsStack = graphicsStack;
        _currentPath = currentPath;
        _graphicsStateOperators = new GraphicsStateOperators(page, processor, operandStack, graphicsStack);
        _textOperators = new TextOperators(renderer, page, processor, operandStack);
        _pathOperators = new PathOperators(renderer, operandStack, processor, currentPath, page);
        _colorOperators = new ColorOperators(operandStack, page);
        _inlineImageOperators = new InlineImageOperators(renderer, operandStack, page, processor);
        _markedContentOperators = new MarkedContentOperators(operandStack, page, processor);
        _miscOperators = new MiscellaneousOperators(renderer, operandStack, page, processor);
        _logger = page.Document.LoggerFactory.CreateLogger<PdfOperatorProcessor>();
    }

    internal static List<IPdfValue> GetOperands(int count, Stack<IPdfValue> operandStack)
    {
        List<IPdfValue> operands = new(count);
        for (int index = 0; index < count && operandStack.Count > 0; index++)
        {
            operands.Insert(0, operandStack.Pop());
        }

        return operands;
    }

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        var handled = false;

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

        if (!handled && _markedContentOperators.CanProcess(op))
        {
            _markedContentOperators.ProcessOperator(op, ref graphicsState);
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

    private void ProcessUnknownOperator(string op) => _logger.LogWarning("Unknown PDF operator {Op}' with {Count} operands on stack", op, _operandStack.Count);
}
