using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using PdfPixel.TextExtraction;
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
                ProcessMarkedContentPoint(graphicsState);
                break;
            }
            case "DP":
            {
                // Marked content point with properties
                ProcessMarkedContentPointWithProperties(graphicsState);
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

    private void ProcessMarkedContentPoint(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString tagName = operands[0].AsName();
        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName, propertiesDictionary: null);
    }

    private void ProcessMarkedContentPointWithProperties(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        PdfString tagName = operands[0].AsName();
        PdfDictionary? propertiesDictionary = ResolvePropertiesDictionary(operands[1]);
        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName, propertiesDictionary);
    }

    private void ProcessBeginMarkedContent()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString tagName = operands[0].AsName();
        PdfMarkedContent markedContent = new(tagName) { TextMarkup = TryParseTextMarkup(tagName, propertiesDictionary: null) };

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private void ProcessEndMarkedContent() => _processor.Process(new EndMarkedContentCommand());

    private void ProcessBeginMarkedContentWithProperties()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        PdfString tagName = operands[0].AsName();
        PdfMarkedContent markedContent = new(tagName);

        if (tagName == PdfTokens.OptionalContentKey)
        {
            PdfObject? propertiesObject = ResolvePropertiesObject(operands[1]);
            if (propertiesObject != null)
            {
                markedContent.OptionalContent = PdfOptionalContentMembership.FromOptionalContentObject(propertiesObject);
            }
        }
        else
        {
            PdfDictionary? propertiesDictionary = ResolvePropertiesDictionary(operands[1]);
            markedContent.TextMarkup = TryParseTextMarkup(tagName, propertiesDictionary);
        }

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private PdfTextMarkup? TryParseTextMarkup(in PdfString tagName, PdfDictionary? propertiesDictionary)
    {
        PdfTextTag tag = tagName.AsEnum<PdfTextTag>();

        PdfString inlineActualText = default;
        PdfString inlineLang = default;
        int? mcid = null;

        if (propertiesDictionary != null)
        {
            inlineActualText = propertiesDictionary.GetString(PdfTokens.ActualTextKey);
            inlineLang = propertiesDictionary.GetString(PdfTokens.LangKey);
            mcid = propertiesDictionary.GetInteger(PdfTokens.MCIDKey);
        }

        PdfStructureElement? structureElement = null;
        if (mcid != null)
        {
            structureElement = _page.Document.StructureTree?.FindByMcid(_page.PageObject.Reference, mcid.Value);
        }

        bool hasProperties = !inlineActualText.IsEmpty || !inlineLang.IsEmpty || structureElement != null;

        if (tag == PdfTextTag.Custom && !hasProperties)
        {
            return null;
        }

        PdfTextMarkup markup = new(tag);

        if (tag == PdfTextTag.Custom)
        {
            markup.CustomTag = tagName;
        }

        if (structureElement != null)
        {
            markup.ActualText = (inlineActualText.IsEmpty) ? structureElement.ActualText : inlineActualText;
            markup.Lang = (inlineLang.IsEmpty) ? structureElement.Lang : inlineLang;
            markup.StructureElement = structureElement;
        }
        else
        {
            markup.ActualText = inlineActualText;
            markup.Lang = inlineLang;
        }

        return markup;
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

    private PdfObject? ResolvePropertiesObject(IPdfValue propertiesOperand)
    {
        // Inline dictionary — wrap in a synthetic PdfObject.
        PdfDictionary? inlineDictionary = propertiesOperand.AsDictionary();
        if (inlineDictionary != null)
        {
            return new PdfObject(default, _page.Document, propertiesOperand);
        }

        // Resource name — look up in /Properties subdictionary.
        PdfString propertiesName = propertiesOperand.AsName();
        if (propertiesName.IsEmpty)
        {
            return null;
        }

        PdfDictionary? properties = _page.ResourceDictionary.GetDictionary(PdfTokens.PropertiesKey);
        return properties?.GetObject(propertiesName);
    }
}
