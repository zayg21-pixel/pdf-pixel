using PdfPixel.Commands.Model;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Tagging.Model;
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

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, propertiesOperand: null, isSequence: false, graphicsState)));
        _processor.Process(new EndScopeCommand());
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

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, operands[1], isSequence: false, graphicsState)));
        _processor.Process(new EndScopeCommand());
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

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, propertiesOperand: null, isSequence: true, graphicsState)));
    }

    private void ProcessEndMarkedContent() => _processor.Process(new EndScopeCommand());

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

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, operands[1], isSequence: true, graphicsState)));
    }

    private PdfMarkedContentScope CreateMarkedContent(in PdfString tagName, IPdfValue? propertiesOperand, bool isSequence, PdfGraphicsState graphicsState)
    {
        PdfMarkedContentTag tagType = tagName.AsEnum<PdfMarkedContentTag>();

        switch (tagType)
        {
            case PdfMarkedContentTag.OptionalContent:
            {
                PdfOptionalContentMembership? optionalContent = null;
                if (propertiesOperand != null)
                {
                    optionalContent = ResolveOptionalContent(propertiesOperand);
                }

                return new PdfMarkedContentScope(tagName, tagType, optionalContent, associatedFiles: null, structureElement: null);
            }
            case PdfMarkedContentTag.AssociatedFiles:
            {
                List<PdfFileSpecification>? associatedFiles = null;
                if (propertiesOperand != null && isSequence)
                {
                    associatedFiles = ResolveAssociatedFiles(propertiesOperand);
                }

                return new PdfMarkedContentScope(tagName, tagType, optionalContent: null, associatedFiles, structureElement: null);
            }
            default:
            {
                PdfDictionary? propertiesDictionary = null;
                if (propertiesOperand != null)
                {
                    propertiesDictionary = ResolvePropertiesDictionary(propertiesOperand);
                }

                return CreateTaggedContent(tagName, tagType, propertiesDictionary, graphicsState);
            }
        }
    }

    private PdfMarkedContentScope CreateTaggedContent(in PdfString tagName, PdfMarkedContentTag tagType, PdfDictionary? propertiesDictionary, PdfGraphicsState graphicsState)
    {
        int? mcid = propertiesDictionary?.GetInteger(PdfTokens.MCIDKey);

        PdfStructureElement? structureElement = null;
        if (mcid != null && graphicsState.RenderingParameters.ExtractContent)
        {
            structureElement = FindStructureParent(mcid.Value);
        }

        PdfMarkedContentScope markedContent = new(tagName, tagType, optionalContent: null, associatedFiles: null, structureElement) { Mcid = mcid };

        if (propertiesDictionary == null)
        {
            return markedContent;
        }

        if (tagType == PdfMarkedContentTag.Artifact)
        {
            markedContent.Artifact = new PdfArtifactProperties(propertiesDictionary);
        }
        else if (tagType == PdfMarkedContentTag.Span)
        {
            markedContent.ActualText = propertiesDictionary.GetString(PdfTokens.ActualTextKey);
            markedContent.Alt = propertiesDictionary.GetString(PdfTokens.AltKey);
            markedContent.ExpandedForm = propertiesDictionary.GetString(PdfTokens.ExpandedFormKey);
            markedContent.Lang = propertiesDictionary.GetString(PdfTokens.LangKey);
        }

        return markedContent;
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

    private List<PdfFileSpecification>? ResolveAssociatedFiles(IPdfValue propertiesOperand)
    {
        PdfArray? inlineArray = propertiesOperand.AsArray();
        if (inlineArray != null)
        {
            return PdfFileSpecification.GetFileSpecifications(inlineArray);
        }

        PdfString? propertiesName = propertiesOperand.AsName();
        if (propertiesName == null)
        {
            return null;
        }

        return _host.Cache.GetAssociatedFiles(propertiesName.Value);
    }

    private PdfOptionalContentMembership? ResolveOptionalContent(IPdfValue propertiesOperand)
    {
        // Inline dictionary — wrap in a synthetic PdfObject.
        PdfDictionary? inlineDictionary = propertiesOperand.AsDictionary();
        if (inlineDictionary != null)
        {
            PdfObject inlineObject = new(null, _host.Document, propertiesOperand);
            return PdfOptionalContentMembership.FromObject(inlineObject);
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
