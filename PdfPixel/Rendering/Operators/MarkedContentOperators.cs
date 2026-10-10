using PdfPixel.Commands.Model;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Tagging.Model;
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
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfStructureTree? _structureTree;

    public MarkedContentOperators(Stack<IPdfValue> operandStack, IPdfContentHost host, IPdfCommandProcessor processor)
    {
        _operandStack = operandStack;
        _host = host;
        _processor = processor;
        _structureTree = host.Document.StructureTree;
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
                ProcessBeginMarkedContent(graphicsState);
                break;
            }
            case "BDC":
            {
                // Begin marked content with properties
                ProcessBeginMarkedContentWithProperties(graphicsState);
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

        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary: null, graphicsState);
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
        graphicsState.PendingTextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary, graphicsState);
    }

    private void ProcessBeginMarkedContent(PdfGraphicsState graphicsState)
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

        PdfMarkedContent markedContent = new(tagName.Value) { TextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary: null, graphicsState) };

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private void ProcessEndMarkedContent() => _processor.Process(new EndMarkedContentCommand());

    private void ProcessBeginMarkedContentWithProperties(PdfGraphicsState graphicsState)
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
            markedContent.TextMarkup = TryParseTextMarkup(tagName.Value, propertiesDictionary, graphicsState);
        }

        _processor.Process(new BeginMarkedContentCommand(markedContent));
    }

    private PdfTextMarkup? TryParseTextMarkup(in PdfString tagName, PdfDictionary? propertiesDictionary, PdfGraphicsState graphicsState)
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

        if (tag == PdfTextTag.Custom && actualText == null && lang == null && mcid == null)
        {
            return null;
        }

        PdfStructureElement? structureElement = null;
        if (mcid != null && graphicsState.RenderingParameters.ExtractText)
        {
            structureElement = FindStructureParent(mcid.Value);
        }

        PdfTextMarkup markup = new(tag)
        {
            ActualText = actualText ?? structureElement?.ActualText,
            Lang = lang ?? structureElement?.Lang,
            Mcid = mcid,
            StructureElement = structureElement
        };

        if (tag == PdfTextTag.Custom)
        {
            markup.CustomTag = tagName;
        }

        return markup;
    }

    private PdfStructureElement? FindStructureParent(int mcid)
    {
        int? structParents = _host.StructParents;
        if (_structureTree == null || structParents == null)
        {
            return null;
        }

        return _structureTree.FindParent(structParents.Value, mcid);
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

        return _host.Cache.GetProperties(propertiesName.Value);
    }

    private PdfOptionalContentMembership? ResolveOptionalContent(IPdfValue propertiesOperand)
    {
        // Inline dictionary — wrap in a synthetic PdfObject.
        PdfDictionary? inlineDictionary = propertiesOperand.AsDictionary();
        if (inlineDictionary != null)
        {
            PdfObject inlineObject = new(default, _host.Document, propertiesOperand);
            return PdfOptionalContentMembership.FromOptionalContentObject(inlineObject);
        }

        // Resource name — look up in /Properties subdictionary.
        PdfString? propertiesName = propertiesOperand.AsName();
        if (propertiesName == null)
        {
            return null;
        }

        return _host.Cache.GetOptionalContent(propertiesName.Value);
    }
}
