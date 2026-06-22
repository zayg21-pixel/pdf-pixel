using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles marked content PDF operators (MP, DP, BMC, BDC, EMC).
/// </summary>
internal class MarkedContentOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        "MP",
        "DP",
        "BMC",
        "BDC",
        "EMC"
    ];

    private readonly Stack<IPdfValue> _operandStack;
    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;

    public MarkedContentOperators(Stack<IPdfValue> operandStack, IPdfPageInternal page, IPdfCommandProcessor processor)
    {
        _operandStack = operandStack;
        _page = page;
        _processor = processor;
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "MP":
            {
                // Marked content point
                ProcessMarkedContentPoint();
                break;
            }
            case "DP":
            {
                // Marked content point with properties
                ProcessMarkedContentPointWithProperties();
                break;
            }
            case "BMC":
            {
                // Begin marked content
                ProcessBeginMarkedContent();
                break;
            }
            case "BDC":
            {
                // Begin marked content with properties
                ProcessBeginMarkedContentWithProperties();
                break;
            }
            case "EMC":
            {
                // End marked content
                ProcessEndMarkedContent();
                break;
            }
        }
    }

    private void ProcessMarkedContentPoint()
    {
        // TODO: MP takes 1 operand (tag name). Used for structure elements (/Span, /P, /Artifact, etc.)
        // in tagged PDF. Should record the tag for accessibility/content extraction when implemented.
        PdfOperatorProcessor.GetOperands(1, _operandStack);
    }

    private void ProcessMarkedContentPointWithProperties()
    {
        // TODO: DP takes 2 operands (tag name, properties dictionary or resource name).
        // Properties may contain /MCID (marked content identifier) for structure tree mapping,
        // /ActualText for Unicode text replacement, or /Lang for language tagging.
        PdfOperatorProcessor.GetOperands(2, _operandStack);
    }

    private void ProcessBeginMarkedContent()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfMarkedContentType type = operands[0].AsName().AsEnum<PdfMarkedContentType>();
        _processor.Process(new BeginMarkedContentCommand(new PdfMarkedContent(type)));
    }

    private void ProcessEndMarkedContent() => _processor.Process(new EndMarkedContentCommand());

    private void ProcessBeginMarkedContentWithProperties()
    {
        // TODO: BDC currently only populates properties for /OC (optional content) tag.
        // Other tags like /Span, /P, /Artifact carry properties such as /MCID, /ActualText,
        // /Lang, /Alt that should be populated on PdfMarkedContent when implemented.
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        PdfString tagName = operands[0].AsName();
        PdfMarkedContentType type = tagName.AsEnum<PdfMarkedContentType>();
        PdfMarkedContent markedContent = new(type);

        if (type == PdfMarkedContentType.OptionalContent)
        {
            PdfDictionary? propertiesDictionary = ResolvePropertiesDictionary(operands[1]);
            if (propertiesDictionary != null)
            {
                markedContent.OptionalContent = PdfOptionalContentMembership.FromOptionalContentDictionary(propertiesDictionary);
            }
        }

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private PdfDictionary? ResolvePropertiesDictionary(IPdfValue propertiesOperand)
    {
        PdfDictionary? inlineDictionary = propertiesOperand.AsDictionary();
        if (inlineDictionary != null)
        {
            return inlineDictionary;
        }

        PdfString propertiesName = propertiesOperand.AsName();
        if (propertiesName.IsEmpty)
        {
            return null;
        }

        PdfDictionary? properties = _page.ResourceDictionary.GetDictionary(PdfTokens.PropertiesKey);
        return properties?.GetDictionary(propertiesName);
    }
}
