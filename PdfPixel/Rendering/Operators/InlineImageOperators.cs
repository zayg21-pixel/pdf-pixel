using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Streams;
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

        PdfImage pdfImage = PdfImage.FromXObject(inlineObject, _page, name: PdfString.Empty, isSoftMask: false);
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
            IPdfValue normalizedValue = NormalizeInlineImageValue(expandedKey, valueValue);

            if (!imageDictionary.HasKey(expandedKey))
            {
                imageDictionary.Set(expandedKey, normalizedValue ?? valueValue);
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

    private IPdfValue NormalizeInlineImageValue(in PdfString expandedKey, IPdfValue value)
    {
        if (expandedKey == PdfTokens.ColorSpaceKey && value.Type == PdfValueType.Name)
        {
            PdfInlineImageColorSpace colorSpace = value.AsName().AsEnum<PdfInlineImageColorSpace>();
            if (colorSpace != PdfInlineImageColorSpace.Unknown)
            {
                switch (colorSpace)
                {
                    case PdfInlineImageColorSpace.DeviceGray:
                        return PdfValueFactory.Name(PdfColorSpaceType.DeviceGray.AsPdfString());

                    case PdfInlineImageColorSpace.DeviceRGB:
                        return PdfValueFactory.Name(PdfColorSpaceType.DeviceRGB.AsPdfString());

                    case PdfInlineImageColorSpace.DeviceCMYK:
                        return PdfValueFactory.Name(PdfColorSpaceType.DeviceCMYK.AsPdfString());

                    case PdfInlineImageColorSpace.Indexed:
                        return PdfValueFactory.Name(PdfColorSpaceType.Indexed.AsPdfString());
                }
            }
        }
        else if (expandedKey == PdfTokens.FilterKey)
        {
            if (value.Type == PdfValueType.Name)
            {
                PdfString filterName = value.AsName();
                if (!filterName.IsEmpty)
                {
                    PdfFilterType filter = ExpandFilterName(filterName);

                    if (filter != PdfFilterType.Unknown)
                    {
                        return PdfValueFactory.Name(filter.AsPdfString());
                    }
                    else
                    {
                        return PdfValueFactory.Name(filterName);
                    }
                }
            }
            else if (value.Type == PdfValueType.Array)
            {
                PdfArray? array = value.AsArray();
                if (array?.Count > 0)
                {
                    List<IPdfValue> newValues = new(array.Count);
                    for (int elementIndex = 0; elementIndex < array.Count; elementIndex++)
                    {
                        IPdfValue? item = array.GetValue(elementIndex);
                        if (item != null && item.Type == PdfValueType.Name)
                        {
                            PdfString itemName = item.AsName();
                            PdfFilterType filterName = ExpandFilterName(itemName);

                            if (filterName != PdfFilterType.Unknown)
                            {
                                newValues.Add(PdfValueFactory.Name(filterName.AsPdfString()));
                            }
                            else
                            {
                                newValues.Add(PdfValueFactory.Name(itemName));
                            }

                        }

                        if (item != null)
                        {
                            newValues.Add(item);
                        }
                    }

                    return PdfValueFactory.Array(new PdfArray(array.Document, newValues));
                }
            }
        }

        return value;
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

    /// <summary>
    /// Expands abbreviated inline image filter names to their full PDF filter names using PdfInlineImageFilter enum.
    /// </summary>
    /// <param name="raw">The raw filter abbreviation (e.g., Fl, LZW, AHx).</param>
    /// <returns>The full filter name as defined in PdfTokens, or the original value if not recognized.</returns>
    private PdfFilterType ExpandFilterName(in PdfString raw)
    {
        PdfInlineImageFilter filter = raw.AsEnum<PdfInlineImageFilter>();
        switch (filter)
        {
            case PdfInlineImageFilter.Flate:
                return PdfFilterType.FlateDecode;

            case PdfInlineImageFilter.LZW:
                return PdfFilterType.LZWDecode;

            case PdfInlineImageFilter.ASCIIHex:
                return PdfFilterType.ASCIIHexDecode;

            case PdfInlineImageFilter.ASCII85:
                return PdfFilterType.ASCII85Decode;

            case PdfInlineImageFilter.RunLength:
                return PdfFilterType.RunLengthDecode;

            case PdfInlineImageFilter.CCITTFax:
                return PdfFilterType.CCITTFaxDecode;

            case PdfInlineImageFilter.DCT:
                return PdfFilterType.DCTDecode;

            case PdfInlineImageFilter.JPX:
                return PdfFilterType.JPXDecode;

            case PdfInlineImageFilter.JBIG2:
                return PdfFilterType.JBIG2Decode;

            default:
                return PdfFilterType.Unknown;
        }
    }
}
