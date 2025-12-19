using Microsoft.Extensions.Logging;
using PdfReader.Forms;
using PdfReader.Imaging.Model;
using PdfReader.Models;
using PdfReader.Rendering.State;
using PdfReader.Shading.Model;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering.Operators;

/// <summary>
/// Handles miscellaneous PDF operators that don't fit into other specialized categories.
/// Includes XObject invocation, marked content, compatibility, Type 3 font metrics, and shading.
/// </summary>
public class MiscellaneousOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = new HashSet<string>
    {
        // XObject invocation
        "Do",
        // Marked content
        "MP","DP","BMC","BDC","EMC",
        // Compatibility
        "BX","EX",
        // Type 3 font metrics
        "d0","d1",
        // Shading
        "sh"
    };

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly PdfPage _page;
    private readonly SKCanvas _canvas;
    private readonly ILogger<MiscellaneousOperators> _logger;

    public MiscellaneousOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, PdfPage page, SKCanvas canvas)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _page = page;
        _canvas = canvas;
        _logger = page.Document.LoggerFactory.CreateLogger<MiscellaneousOperators>();
    }

    public bool CanProcess(string op)
    {
        return SupportedOperators.Contains(op);
    }

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "Do":
            {
                ProcessInvokeXObject(graphicsState);
                break;
            }
            case "MP":
            {
                ProcessMarkContentPoint();
                break;
            }
            case "DP":
            {
                ProcessMarkContentPointWithProperties();
                break;
            }
            case "BMC":
            {
                ProcessBeginMarkedContent();
                break;
            }
            case "BDC":
            {
                ProcessBeginMarkedContentWithProperties();
                break;
            }
            case "EMC":
            {
                ProcessEndMarkedContent();
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
                ProcessSetGlyphWidth(ref graphicsState);
                break;
            }
            case "d1":
            {
                ProcessSetGlyphWidthAndBoundingBox(ref graphicsState);
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
        var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        var xObjectName = operands[0].AsName();
        if (xObjectName.IsEmpty)
        {
            return;
        }

        var pageObject = _page.Cache.GetXObject(xObjectName);

        if (pageObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' not found in resources", xObjectName);
            return;
        }

        switch (pageObject.Subtype)
        {
            case PdfXObjectSubtype.Image:
            {
                var pdfImage = PdfImage.FromXObject(pageObject.XObject, _page, xObjectName, isSoftMask: false);
                _renderer.DrawImage(_canvas, pdfImage, graphicsState);
                break;
            }
            case PdfXObjectSubtype.Form:
                var formXObject = PdfForm.FromXObject(pageObject.XObject, _page);
                _renderer.DrawForm(_canvas, formXObject, graphicsState);
                break;
            default:
                _logger.LogWarning("Unsupported XObject subtype '{XObjectSubtype}' for XObject '{XObjectName}'", pageObject.Subtype, xObjectName);
                return;
        }
    }

    // Handles MP (Marked Content Point): expects 1 operand (tag name)
    private void ProcessMarkContentPoint()
    {
        // PDF spec: MP operator takes a single tag name operand.
        // Operand is ignored for rendering, but must be popped to maintain stack integrity.
        PdfOperatorProcessor.GetOperands(1, _operandStack); // Intentionally ignored.
    }

    // Handles DP (Marked Content Point with Properties): expects 2 operands (tag name, property dictionary)
    private void ProcessMarkContentPointWithProperties()
    {
        // PDF spec: DP operator takes a tag name and a property dictionary.
        // Operands are ignored for rendering, but must be popped to maintain stack integrity.
        PdfOperatorProcessor.GetOperands(2, _operandStack); // Intentionally ignored.
    }

    // Handles BMC (Begin Marked Content): expects 1 operand (tag name)
    private void ProcessBeginMarkedContent()
    {
        // PDF spec: BMC operator takes a single tag name operand.
        // Operand is ignored for rendering, but must be popped to maintain stack integrity.
        PdfOperatorProcessor.GetOperands(1, _operandStack); // Intentionally ignored.
    }

    // Handles BDC (Begin Marked Content with Properties): expects 2 operands (tag name, property dictionary)
    private void ProcessBeginMarkedContentWithProperties()
    {
        // PDF spec: BDC operator takes a tag name and a property dictionary.
        // Operands are ignored for rendering, but must be popped to maintain stack integrity.
        PdfOperatorProcessor.GetOperands(2, _operandStack); // Intentionally ignored.
    }

    // Handles EMC (End Marked Content): no operands
    private void ProcessEndMarkedContent()
    {
        // PDF spec: EMC operator takes no operands.
        // No state to manage presently.
    }

    private void ProcessBeginCompatibility()
    {
        // Ignored per PDF spec (compatibility section start).
    }

    private void ProcessEndCompatibility()
    {
        // Ignored per PDF spec (compatibility section end).
    }

    private void ProcessSetGlyphWidth(ref PdfGraphicsState graphicsState)
    {
        var operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        var wx = operands[0].AsFloat();
        var wy = operands[1].AsFloat();
        graphicsState.Type3Advancement = new SKSize(wx, wy);
        graphicsState.Type3BoundingBox = null; // d0 implies colored glyph; no bbox supplied
    }

    private void ProcessSetGlyphWidthAndBoundingBox(ref PdfGraphicsState graphicsState)
    {
        var operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
        if (operands.Count < 6)
        {
            return;
        }

        var wx = operands[0].AsFloat();
        var wy = operands[1].AsFloat();
        var llx = operands[2].AsFloat();
        var lly = operands[3].AsFloat();
        var urx = operands[4].AsFloat();
        var ury = operands[5].AsFloat();

        graphicsState.Type3Advancement = new SKSize(wx, wy);
        graphicsState.Type3BoundingBox = new SKRect(llx, lly, urx, ury);
    }

    private void ProcessShading(PdfGraphicsState graphicsState)
    {
        var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        var shadingName = operands[0].AsName();
        if (shadingName.IsEmpty)
        {
            return;
        }

        var shadings = _page.ResourceDictionary.GetDictionary(PdfTokens.ShadingKey);
        var shadingObject = shadings?.GetObject(shadingName);

        if (shadingObject == null)
        {
            _logger.LogWarning("Shading '{ShadingName}' not found in resources", shadingName);
            return;
        }

        var shading = new PdfShading(shadingObject, _page);

        _renderer.DrawShading(_canvas, shading, graphicsState);
    }
}