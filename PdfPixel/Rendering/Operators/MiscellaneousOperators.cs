using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles miscellaneous PDF operators that don't fit into other specialized categories.
/// Includes XObject invocation, compatibility, Type 3 font metrics, and shading.
/// </summary>
internal class MiscellaneousOperators : IOperatorProcessor
{
    private readonly IPdfRenderer _renderer;
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<MiscellaneousOperators> _logger;

    public MiscellaneousOperators(IPdfRenderer renderer, PdfOperandStack operandStack, IPdfContentHost host, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _host = host;
        _processor = processor;
        _logger = host.Document.LoggerFactory.CreateLogger<MiscellaneousOperators>();
    }

    public bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.InvokeXObject:
            {
                ProcessInvokeXObject(context, graphicsState);
                break;
            }
            case PdfOperator.BeginCompatibility:
            {
                ProcessBeginCompatibility();
                break;
            }
            case PdfOperator.EndCompatibility:
            {
                ProcessEndCompatibility();
                break;
            }
            case PdfOperator.GlyphWidth:
            {
                // handled by type 3 font.
                break;
            }
            case PdfOperator.GlyphWidthAndBoundingBox:
            {
                // handled by type 3 font.
                break;
            }
            case PdfOperator.Shading:
            {
                ProcessShading(context, graphicsState);
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void ProcessInvokeXObject(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? xObjectName = operands[0].GetPdfValue(context).AsName();
        if (xObjectName == null)
        {
            return;
        }

        PdfXObject? pageObject = _host.Resources.GetXObject(xObjectName.Value);

        if (pageObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' not found in resources", xObjectName);
            return;
        }

        switch (pageObject)
        {
            case PdfImage pdfImage:
            {
                DrawImage(pdfImage, graphicsState);
                break;
            }
            case PdfForm formXObject:
            {
                _renderer.DrawForm(_processor, formXObject, graphicsState);
                break;
            }
        }
    }

    private void DrawImage(PdfImage pdfImage, PdfGraphicsState graphicsState)
    {
        PdfStructureTree? structureTree = null;
        if (graphicsState.RenderingParameters.ExtractContent)
        {
            structureTree = _host.Document.StructureTree;
        }

        PdfXObjectScope? scope = PdfXObjectScope.FromXObject(pdfImage, structureTree);
        if (scope != null)
        {
            _processor.Process(new BeginScopeCommand(scope));
        }

        _renderer.DrawImage(_processor, pdfImage, graphicsState);

        if (scope != null)
        {
            _processor.Process(new EndScopeCommand());
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

    private void ProcessShading(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? shadingName = operands[0].GetPdfValue(context).AsName();
        if (shadingName == null)
        {
            return;
        }

        if (!graphicsState.RenderingParameters.ProcessShadings
            || (!graphicsState.RenderingParameters.RenderContent && !graphicsState.RenderingParameters.ExtractContent))
        {
            return;
        }

        PdfShading? shading = _host.Resources.GetShading(shadingName.Value);

        if (shading == null)
        {
            return;
        }

        _renderer.DrawShading(_processor, shading, graphicsState);
    }
}
