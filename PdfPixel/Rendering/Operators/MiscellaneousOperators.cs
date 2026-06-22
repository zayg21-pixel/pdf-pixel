using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles miscellaneous PDF operators that don't fit into other specialized categories.
/// Includes XObject invocation, compatibility, Type 3 font metrics, and shading.
/// </summary>
internal class MiscellaneousOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        // XObject invocation
        "Do",
        // Compatibility
        "BX",
        "EX",
        // Type 3 font metrics
        "d0",
        "d1",
        // Shading
        "sh"
    ];

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<MiscellaneousOperators> _logger;

    public MiscellaneousOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, IPdfPageInternal page, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _page = page;
        _processor = processor;
        _logger = page.Document.LoggerFactory.CreateLogger<MiscellaneousOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "Do":
            {
                ProcessInvokeXObject(graphicsState);
                break;
            }
            case "BX":
            {
                ProcessBeginCompatibility();
                break;
            }
            case "EX":
            {
                ProcessEndCompatibility();
                break;
            }
            case "d0":
            {
                // handled by type 3 font.
                break;
            }
            case "d1":
            {
                // handled by type 3 font.
                break;
            }
            case "sh":
            {
                ProcessShading(graphicsState);
                break;
            }
        }
    }

    private void ProcessInvokeXObject(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString xObjectName = operands[0].AsName();
        if (xObjectName.IsEmpty)
        {
            return;
        }

        PdfXObject? pageObject = _page.Cache.GetXObject(xObjectName);

        if (pageObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' not found in resources", xObjectName);
            return;
        }

        PdfOptionalContentMembership? optionalContent = pageObject.OptionalContent;
        if (optionalContent != null)
        {
            PdfMarkedContent markedContent = new(PdfMarkedContentType.OptionalContent) { OptionalContent = optionalContent };
            _processor.Process(new BeginMarkedContentCommand(markedContent));
        }

        switch (pageObject.Subtype)
        {
            case PdfXObjectSubtype.Image:
            {
                PdfImage pdfImage = PdfImage.FromXObject(pageObject.XObject, _page, xObjectName, isSoftMask: false);
                _renderer.DrawImage(_processor, pdfImage, graphicsState);
                break;
            }
            case PdfXObjectSubtype.Form:
            {
                PdfForm formXObject = PdfForm.FromXObject(pageObject.XObject, _page);
                _renderer.DrawForm(_processor, formXObject, graphicsState);
                break;
            }
            default:
            {
                _logger.LogWarning("Unsupported XObject subtype '{XObjectSubtype}' for XObject '{XObjectName}'", pageObject.Subtype, xObjectName);
                break;
            }
        }

        if (optionalContent != null)
        {
            _processor.Process(new EndMarkedContentCommand());
        }
    }

    private void ProcessBeginCompatibility()
    {
        // Ignored per PDF spec (compatibility section start).
    }

    private void ProcessEndCompatibility()
    {
        // Ignored per PDF spec (compatibility section end).
    }

    private void ProcessShading(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString shadingName = operands[0].AsName();
        if (shadingName.IsEmpty)
        {
            return;
        }

        PdfDictionary? shadings = _page.ResourceDictionary.GetDictionary(PdfTokens.ShadingKey);
        PdfObject? shadingObject = shadings?.GetObject(shadingName);

        if (shadingObject == null)
        {
            _logger.LogWarning("Shading '{ShadingName}' not found in resources", shadingName);
            return;
        }

        PdfShading shading = new(shadingObject);

        _renderer.DrawShading(_processor, shading, graphicsState);
    }
}
