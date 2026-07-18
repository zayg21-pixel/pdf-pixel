using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles inline image operators (BI / ID / EI). Parameter collection, dictionary expansion and image data
/// scanning happen once in <see cref="PdfPixel.Parsing.PdfParser"/> while the ID token is processed; by the
/// time EI fires, the fully parsed image is already sitting on the operand stack.
/// </summary>
internal class InlineImageOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        "BI", "ID", "EI"
    ];

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<InlineImageOperators> _logger;

    public InlineImageOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, IPdfPageInternal page, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _processor = processor;
        _logger = page.Document.LoggerFactory.CreateLogger<InlineImageOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "BI":
            {
                // Begin Inline Image - no action needed, parameters will be collected on stack
                break;
            }
            case "ID":
            {
                // Inline Image - comsumed by EI operator, so no action needed here
                break;
            }
            case "EI":
            {
                ProcessEndInlineImage(ref graphicsState);
                break;
            }
        }
    }

    private void ProcessEndInlineImage(ref PdfGraphicsState graphicsState)
    {
        IPdfValue value = _operandStack.Pop();
        _operandStack.Clear();

        PdfObject? inlineObject = value.AsInlineImage();
        if (inlineObject == null)
        {
            return;
        }

        PdfImage pdfImage = PdfImage.GetImage(inlineObject);
        _renderer.DrawImage(_processor, pdfImage, graphicsState);
    }
}
