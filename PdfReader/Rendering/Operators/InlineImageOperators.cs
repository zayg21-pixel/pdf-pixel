using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Color.ColorSpace;
using PdfReader.Imaging.Model;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.State;
using PdfReader.Streams;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles inline image operators (BI / ID / EI) including parameter collection and image decoding.
    /// Requires access to the raw parse context for scanning inline image data bytes.
    /// </summary>
    internal class InlineImageOperators : IOperatorProcessor
    {
        private static readonly HashSet<string> SupportedOperators = new HashSet<string>
        {
            "BI", "ID", "EI"
        };

        private readonly Stack<IPdfValue> _operandStack;
        private readonly PdfPage _page;
        private readonly SKCanvas _canvas;
        private readonly ILogger<InlineImageOperators> _logger;

        public InlineImageOperators(Stack<IPdfValue> operandStack, PdfPage page, SKCanvas canvas)
        {
            _operandStack = operandStack;
            _page = page;
            _canvas = canvas;
            _logger = page.Document.LoggerFactory.CreateLogger<InlineImageOperators>();
        }

        public bool CanProcess(string op)
        {
            return SupportedOperators.Contains(op);
        }

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
            try
            {
                var image = _operandStack.Pop();
                var parameterValues = new List<IPdfValue>(_operandStack);
                parameterValues.Reverse();
                _operandStack.Clear();

                var imageDictionary = BuildImageDictionary(parameterValues);
                if (imageDictionary == null)
                {
                    return;
                }


                var inlineObject = new PdfObject(new PdfReference(-1), _page.Document, PdfValue.Dictionary(imageDictionary))
                {
                    EmbaddedStream = image.AsString().Value
                };

                var pdfImage = PdfImage.FromXObject(inlineObject, _page, name: PdfString.Empty, isSoftMask: false);
                _page.Document.PdfRenderer.DrawUnitImage(_canvas, pdfImage, graphicsState, _page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inline image");
            }
        }

        private PdfDictionary BuildImageDictionary(List<IPdfValue> parameters)
        {
            var imageDictionary = new PdfDictionary(_page.Document);
            imageDictionary.Set(PdfTokens.SubtypeKey, PdfValue.Name(PdfTokens.ImageSubtype));

            for (int parameterIndex = 0; parameterIndex + 1 < parameters.Count; parameterIndex += 2)
            {
                var keyValue = parameters[parameterIndex];
                var valueValue = parameters[parameterIndex + 1];
                if (keyValue.Type != PdfValueType.Name)
                {
                    break;
                }

                var rawKey = keyValue.AsName();
                if (rawKey.IsEmpty)
                {
                    continue;
                }

                var expandedKey = ExpandInlineImageKey(rawKey);
                var normalizedValue = NormalizeInlineImageValue(expandedKey, valueValue);

                if (!imageDictionary.HasKey(expandedKey))
                {
                    imageDictionary.Set(expandedKey, normalizedValue ?? valueValue);
                }
            }

            if (!imageDictionary.HasKey(PdfTokens.BitsPerComponentKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
            {
                imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValue.Integer(8));
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
                imageDictionary.Set(PdfTokens.ColorSpaceKey, PdfValue.Name(PdfColorSpaceType.DeviceGray.AsPdfString()));
            }
            if (imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey) && !imageDictionary.HasKey(PdfTokens.BitsPerComponentKey))
            {
                imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValue.Integer(1));
            }

            return imageDictionary;
        }

        private IPdfValue NormalizeInlineImageValue(PdfString expandedKey, IPdfValue value)
        {
            if (expandedKey == PdfTokens.ColorSpaceKey && value.Type == PdfValueType.Name)
            {
                var colorSpace = value.AsName().AsEnum<PdfInlineImageColorSpace>();
                if (colorSpace != PdfInlineImageColorSpace.Unknown)
                {
                    switch (colorSpace)
                    {
                        case PdfInlineImageColorSpace.DeviceGray: return PdfValue.Name(PdfColorSpaceType.DeviceGray.AsPdfString());
                        case PdfInlineImageColorSpace.DeviceRGB: return PdfValue.Name(PdfColorSpaceType.DeviceRGB.AsPdfString());
                        case PdfInlineImageColorSpace.DeviceCMYK: return PdfValue.Name(PdfColorSpaceType.DeviceCMYK.AsPdfString());
                        case PdfInlineImageColorSpace.Indexed: return PdfValue.Name(PdfColorSpaceType.Indexed.AsPdfString());
                    }
                }
            }
            else if (expandedKey == PdfTokens.FilterKey)
            {
                if (value.Type == PdfValueType.Name)
                {
                    var filterName = value.AsName();
                    if (!filterName.IsEmpty)
                    {
                        var filter = ExpandFilterName(filterName);

                        if (filter != PdfFilterType.Unknown)
                        {
                            return PdfValue.Name(filter.AsPdfString());
                        }
                        else
                        {
                            return PdfValue.Name(filterName);
                        }
                    }
                }
                else if (value.Type == PdfValueType.Array)
                {
                    var array = value.AsArray();
                    if (array != null && array.Count > 0)
                    {
                        var newValues = new List<IPdfValue>(array.Count);
                        for (int elementIndex = 0; elementIndex < array.Count; elementIndex++)
                        {
                            var item = array.GetValue(elementIndex);
                            if (item != null && item.Type == PdfValueType.Name)
                            {
                                var itemName = item.AsName();
                                var filterName = ExpandFilterName(itemName);

                                if (filterName != PdfFilterType.Unknown)
                                {
                                    newValues.Add(PdfValue.Name(filterName.AsPdfString()));
                                }
                                else
                                {
                                    newValues.Add(PdfValue.Name(itemName));
                                }

                            }
                            newValues.Add(item);
                        }
                        return PdfValue.Array(new PdfArray(array.Document, newValues));
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
        private PdfString ExpandInlineImageKey(PdfString key)
        {
            var property = key.AsEnum<PdfInlineImageProperty>();
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
        private PdfFilterType ExpandFilterName(PdfString raw)
        {
            var filter = raw.AsEnum<PdfInlineImageFilter>();
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
}
