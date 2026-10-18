using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
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
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfOperandStack _operandStack;
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
        IPdfContentHost host,
        IPdfCommandProcessor processor,
        PdfOperandStack operandStack,
        Stack<PdfGraphicsState> graphicsStack,
        PdfPathBuilder currentPath)
    {
        _renderer = renderer;
        _host = host;
        _processor = processor;
        _operandStack = operandStack;
        _graphicsStack = graphicsStack;
        _currentPath = currentPath;
        _graphicsStateOperators = new GraphicsStateOperators(host, processor, operandStack, graphicsStack);
        _textOperators = new TextOperators(renderer, host, processor, operandStack);
        _pathOperators = new PathOperators(renderer, operandStack, processor, currentPath, host);
        _colorOperators = new ColorOperators(operandStack, host);
        _inlineImageOperators = new InlineImageOperators(renderer, operandStack, host, processor);
        _markedContentOperators = new MarkedContentOperators(operandStack, host, processor);
        _miscOperators = new MiscellaneousOperators(renderer, operandStack, host, processor);
        _logger = host.Document.LoggerFactory.CreateLogger<PdfOperatorProcessor>();
    }

    public void ProcessOperator(PdfOperator contentOperator, ref PdfGraphicsState graphicsState)
    {
        bool handled = _pathOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _graphicsStateOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _colorOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _textOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _markedContentOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _inlineImageOperators.ProcessOperator(contentOperator, ref graphicsState)
            || _miscOperators.ProcessOperator(contentOperator, ref graphicsState);

        if (!handled)
        {
            ProcessUnknownOperator(contentOperator);
        }
    }

    private void ProcessUnknownOperator(PdfOperator contentOperator)
        => _logger.LogWarning("Unknown PDF operator {Op}' with {Count} operands on stack", PdfOperatorValue.FromOperator(contentOperator).GetOperatorText(), _operandStack.Count);
}
