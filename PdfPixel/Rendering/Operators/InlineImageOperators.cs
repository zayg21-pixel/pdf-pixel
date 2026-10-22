using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles inline image operators (BI / ID / EI). ID reads the image, from the entries on the operand stack and the data
/// that follows in the content stream, with <see cref="PdfImage.FromInline"/>; EI draws it.
/// </summary>
internal class InlineImageOperators : IOperatorProcessor
{
    private readonly PdfRenderer _renderer;
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<InlineImageOperators> _logger;
    private PdfImage? _inlineImage;

    public InlineImageOperators(PdfRenderer renderer, PdfOperandStack operandStack, IPdfContentHost host, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _processor = processor;
        _logger = host.Document.LoggerFactory.CreateLogger<InlineImageOperators>();
    }

    public bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.BeginInlineImage:
            {
                _inlineImage = null;
                break;
            }
            case PdfOperator.InlineImageData:
            {
                // The data is read even when images are not processed, so the stream resumes past it.
                _inlineImage = PdfImage.FromInline(context, _operandStack);
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
        PdfImage? inlineImage = _inlineImage;
        _inlineImage = null;

        if (inlineImage == null
            || !graphicsState.RenderingParameters.ProcessImages
            || (!graphicsState.RenderingParameters.RenderContent && !graphicsState.RenderingParameters.ExtractContent))
        {
            return;
        }

        _renderer.DrawImage(_processor, inlineImage, graphicsState);
    }
}
