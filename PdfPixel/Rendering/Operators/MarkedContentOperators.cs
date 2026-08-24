using PdfPixel.Commands.Model;
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

        PdfString? tagName = operands[0].AsName();
        if (tagName == null)
        {
            return;
        }

        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary: null);
    }

    private void ProcessMarkedContentPointWithProperties(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        PdfString? tagName = operands[0].AsName();
        if (tagName == null)
        {
            return;
        }

        PdfDictionary? propertiesDictionary = ResolvePropertiesDictionary(operands[1]);
        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary);
    }

    private void ProcessBeginMarkedContent()
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString? tagName = operands[0].AsName();
        if (tagName == null)
        {
            return;
        }

        PdfMarkedContent markedContent = new(tagName.Value) { TextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary: null) };

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

        PdfString? tagName = operands[0].AsName();
        if (tagName == null)
        {
            return;
        }

        PdfMarkedContent markedContent = new(tagName.Value);

        if (tagName.Value == PdfTokens.OptionalContentKey)
        {
            markedContent.OptionalContent = ResolveOptionalContent(operands[1]);
        }
        else
        {
            PdfDictionary? propertiesDictionary = ResolvePropertiesDictionary(operands[1]);
            markedContent.TextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary);
        }

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private PdfTextMarkup? TryParseTextMarkup(in PdfString tagName, PdfDictionary? propertiesDictionary)
    {
        PdfTextTag tag = tagName.AsEnum<PdfTextTag>();

        PdfString? actualText = null;
        PdfString? lang = null;
        int? mcid = null;

        if (propertiesDictionary != null)
        {
            actualText = propertiesDictionary.GetString(PdfTokens.ActualTextKey);
            lang = propertiesDictionary.GetString(PdfTokens.LangKey);
            mcid = propertiesDictionary.GetInteger(PdfTokens.MCIDKey);
        }

        // TODO: [MEDIUM] fall back to the structure element the MCID belongs to for /ActualText and /Lang,
        // resolving it through the structure tree's /ParentTree entry named by /StructParents

        if (tag == PdfTextTag.Custom && actualText == null && lang == null && mcid == null)
        {
            return null;
        }

        PdfTextMarkup markup = new(tag)
        {
            ActualText = actualText,
            Lang = lang,
            Mcid = mcid
        };

        if (tag == PdfTextTag.Custom)
        {
            markup.CustomTag = tagName;
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

        PdfString? propertiesName = propertiesOperand.AsName();
        if (propertiesName == null)
        {
            return null;
        }

        return _page.Cache.GetProperties(propertiesName.Value);
    }

    private PdfOptionalContentMembership? ResolveOptionalContent(IPdfValue propertiesOperand)
    {
        // Inline dictionary — wrap in a synthetic PdfObject.
        PdfDictionary? inlineDictionary = propertiesOperand.AsDictionary();
        if (inlineDictionary != null)
        {
            PdfObject inlineObject = new(default, _page.Document, propertiesOperand);
            return PdfOptionalContentMembership.FromOptionalContentObject(inlineObject);
        }

        // Resource name — look up in /Properties subdictionary.
        PdfString? propertiesName = propertiesOperand.AsName();
        if (propertiesName == null)
        {
            return null;
        }

        return _page.Cache.GetOptionalContent(propertiesName.Value);
    }
}
