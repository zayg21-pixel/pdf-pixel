using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Parsing;
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
    private readonly IPdfRenderer _renderer;
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<InlineImageOperators> _logger;

    public InlineImageOperators(IPdfRenderer renderer, PdfOperandStack operandStack, IPdfContentHost host, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _processor = processor;
        _logger = host.Document.LoggerFactory.CreateLogger<InlineImageOperators>();
    }

    public bool ProcessOperator(PdfOperator op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.BeginInlineImage:
            {
                // Begin Inline Image - no action needed, parameters will be collected on stack
                break;
            }
            case PdfOperator.InlineImageData:
            {
                // Inline Image - comsumed by EI operator, so no action needed here
                break;
            }
            case PdfOperator.EndInlineImage:
            {
                ProcessEndInlineImage(ref graphicsState);
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void ProcessEndInlineImage(ref PdfGraphicsState graphicsState)
    {
        PdfValue value = _operandStack.Pop();
        _operandStack.Clear();

        if (!graphicsState.RenderingParameters.ProcessImages
            || (!graphicsState.RenderingParameters.RenderContent && !graphicsState.RenderingParameters.ExtractContent))
        {
            return;
        }

        PdfObject? inlineObject = value.AsInlineImage();
        if (inlineObject == null)
        {
            return;
        }

        PdfImage pdfImage = PdfImage.FromInlineObject(inlineObject);
        _renderer.DrawImage(_processor, pdfImage, graphicsState);
    }
}
