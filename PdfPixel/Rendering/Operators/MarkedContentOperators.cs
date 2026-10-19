using PdfPixel.Commands.Model;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles marked content PDF operators (MP, DP, BMC, BDC, EMC).
/// </summary>
internal class MarkedContentOperators : IOperatorProcessor
{
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfStructureTree _structureTree;

    public MarkedContentOperators(PdfOperandStack operandStack, IPdfContentHost host, IPdfCommandProcessor processor)
    {
        _operandStack = operandStack;
        _host = host;
        _processor = processor;
        _structureTree = host.Document.StructureTree;
    }

    public bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.MarkedContentPoint:
            {
                // Marked content point
                ProcessMarkedContentPoint(context, graphicsState);
                break;
            }
            case PdfOperator.MarkedContentPointWithProperties:
            {
                // Marked content point with properties
                ProcessMarkedContentPointWithProperties(context, graphicsState);
                break;
            }
            case PdfOperator.BeginMarkedContent:
            {
                // Begin marked content
                ProcessBeginMarkedContent(context, graphicsState);
                break;
            }
            case PdfOperator.BeginMarkedContentWithProperties:
            {
                // Begin marked content with properties
                ProcessBeginMarkedContentWithProperties(context, graphicsState);
                break;
            }
            case PdfOperator.EndMarkedContent:
            {
                // End marked content
                ProcessEndMarkedContent();
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void ProcessMarkedContentPoint(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? tagName = operands[0].GetPdfValue(context).AsName();
        if (tagName == null)
        {
            return;
        }

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, propertiesOperand: null, isSequence: false, graphicsState)));
        _processor.Process(new EndScopeCommand());
    }

    private void ProcessMarkedContentPointWithProperties(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
        {
            return;
        }

        PdfString? tagName = operands[0].GetPdfValue(context).AsName();
        if (tagName == null)
        {
            return;
        }

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, operands[1].GetPdfValue(context),isSequence: false, graphicsState)));
        _processor.Process(new EndScopeCommand());
    }

    private void ProcessBeginMarkedContent(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? tagName = operands[0].GetPdfValue(context).AsName();
        if (tagName == null)
        {
            return;
        }

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, propertiesOperand: null, isSequence: true, graphicsState)));
    }

    private void ProcessEndMarkedContent() => _processor.Process(new EndScopeCommand());

    private void ProcessBeginMarkedContentWithProperties(PdfParseContext context, PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
        {
            return;
        }

        PdfString? tagName = operands[0].GetPdfValue(context).AsName();
        if (tagName == null)
        {
            return;
        }

        _processor.Process(new BeginScopeCommand(CreateMarkedContent(tagName.Value, operands[1].GetPdfValue(context),isSequence: true, graphicsState)));
    }

    private PdfMarkedContentScope CreateMarkedContent(in PdfString tagName, PdfValue? propertiesOperand, bool isSequence, PdfGraphicsState graphicsState)
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

        return new PdfMarkedContentScope(tagName, tagType, mcid, propertiesDictionary, structureElement);
    }

    private PdfStructureElement? FindStructureParent(int mcid)
    {
        int? structParents = _host.StructParents;
        if (structParents == null)
        {
            return null;
        }

        return _structureTree.FindParent(structParents.Value, mcid);
    }

    private PdfDictionary? ResolvePropertiesDictionary(PdfValue propertiesOperand)
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

        return _host.Resources.GetProperties(propertiesName.Value);
    }

    private List<PdfFileSpecification>? ResolveAssociatedFiles(PdfValue propertiesOperand)
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

        return _host.Resources.GetAssociatedFiles(propertiesName.Value);
    }

    private PdfOptionalContentMembership? ResolveOptionalContent(PdfValue propertiesOperand)
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

        return _host.Resources.GetOptionalContent(propertiesName.Value);
    }
}
