using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles inline image operators (BI / ID / EI) including parameter collection and image decoding.
/// Requires access to the raw parse context for scanning inline image data bytes.
/// </summary>
internal class InlineImageOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        "BI", "ID", "EI"
    ];

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;
    private readonly ILogger<InlineImageOperators> _logger;

    public InlineImageOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, IPdfPageInternal page, IPdfCommandProcessor processor)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _page = page;
        _processor = processor;
        _logger = page.Document.LoggerFactory.CreateLogger<InlineImageOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "BI":
            {
                // Begin Inline Image - no action needed, parameters will be collected on stack
                break;
            }
            case "ID":
            {
                // Inline Image - comsumed by EI operator, so no action needed here
                break;
            }
            case "EI":
            {
                ProcessEndInlineImage(ref graphicsState);
                break;
            }
        }
    }

    private void ProcessEndInlineImage(ref PdfGraphicsState graphicsState)
    {
        IPdfValue image = _operandStack.Pop();
        List<IPdfValue> parameterValues = new(_operandStack);
        parameterValues.Reverse();
        _operandStack.Clear();

        PdfDictionary? imageDictionary = BuildImageDictionary(parameterValues);
        if (imageDictionary == null)
        {
            return;
        }

        PdfObject inlineObject = new(default, _page.Document, PdfValueFactory.Dictionary(imageDictionary)) { EmbaddedStream = image.AsString().Value };

        PdfImage pdfImage = PdfImage.GetImage(inlineObject);
        _renderer.DrawImage(_processor, pdfImage, graphicsState);
    }

    private PdfDictionary? BuildImageDictionary(List<IPdfValue> parameters)
    {
        PdfDictionary imageDictionary = new(_page.Document);
        imageDictionary.Set(PdfTokens.SubtypeKey, PdfValueFactory.Name(PdfTokens.ImageSubtype));

        for (int parameterIndex = 0; parameterIndex + 1 < parameters.Count; parameterIndex += 2)
        {
            IPdfValue keyValue = parameters[parameterIndex];
            IPdfValue valueValue = parameters[parameterIndex + 1];
            if (keyValue.Type != PdfValueType.Name)
            {
                break;
            }

            PdfString rawKey = keyValue.AsName();
            if (rawKey.IsEmpty)
            {
                continue;
            }

            PdfString expandedKey = ExpandInlineImageKey(rawKey);

            if (!imageDictionary.HasKey(expandedKey))
            {
                imageDictionary.Set(expandedKey, valueValue);
            }
        }

        if (!imageDictionary.HasKey(PdfTokens.BitsPerComponentKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
        {
            imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValueFactory.Integer(8));
        }

        if (!imageDictionary.HasKey(PdfTokens.WidthKey))
        {
            _logger.LogWarning("Inline image missing /Width – skipping");
            return null;
        }

        if (!imageDictionary.HasKey(PdfTokens.HeightKey))
        {
            _logger.LogWarning("Inline image missing /Height – skipping");
            return null;
        }

        if (!imageDictionary.HasKey(PdfTokens.ColorSpaceKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
        {
            imageDictionary.Set(PdfTokens.ColorSpaceKey, PdfValueFactory.Name(PdfColorSpaceType.DeviceGray.AsPdfString()));
        }

        if (imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey) && !imageDictionary.HasKey(PdfTokens.BitsPerComponentKey))
        {
            imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValueFactory.Integer(1));
        }

        return imageDictionary;
    }

    /// <summary>
    /// Expands abbreviated inline image property keys to their full PDF dictionary keys using PdfInlineImageProperty enum.
    /// </summary>
    /// <param name="key">The raw property key (e.g., /W, /H, /BPC).</param>
    /// <returns>The expanded PDF dictionary key, or the original key if not recognized.</returns>
    private PdfString ExpandInlineImageKey(in PdfString key)
    {
        PdfInlineImageProperty property = key.AsEnum<PdfInlineImageProperty>();
        switch (property)
        {
            case PdfInlineImageProperty.Width:
                return PdfTokens.WidthKey;

            case PdfInlineImageProperty.Height:
                return PdfTokens.HeightKey;

            case PdfInlineImageProperty.BitsPerComponent:
                return PdfTokens.BitsPerComponentKey;

            case PdfInlineImageProperty.ColorSpace:
                return PdfTokens.ColorSpaceKey;

            case PdfInlineImageProperty.Decode:
                return PdfTokens.DecodeKey;

            case PdfInlineImageProperty.DecodeParms:
                return PdfTokens.DecodeParmsKey;

            case PdfInlineImageProperty.Filter:
                return PdfTokens.FilterKey;

            case PdfInlineImageProperty.ImageMask:
                return PdfTokens.ImageMaskKey;

            default:
                return key;
        }
    }

}
