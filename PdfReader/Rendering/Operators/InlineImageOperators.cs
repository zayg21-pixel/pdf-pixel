using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.Image;
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

        public void ProcessOperator(string op, ref PdfParseContext parseContext, ref PdfGraphicsState graphicsState)
        {
            switch (op)
            {
                case "BI":
                {
                    ProcessBeginInlineImage();
                    break;
                }
                case "ID":
                {
                    ProcessInlineImageData(ref parseContext, graphicsState);
                    break;
                }
                case "EI":
                {
                    ProcessUnexpectedEndInlineImage();
                    break;
                }
            }
        }

        private void ProcessBeginInlineImage()
        {
            // Marker only – parameters accumulate on operand stack until ID.
        }

        private void ProcessInlineImageData(ref PdfParseContext parseContext, PdfGraphicsState graphicsState)
        {
            try
            {
                var parameterValues = new List<IPdfValue>(_operandStack);
                parameterValues.Reverse();
                _operandStack.Clear();

                var imageDictionary = BuildImageDictionary(parameterValues);
                if (imageDictionary == null)
                {
                    return;
                }

                SkipSingleWhitespaceAfterId(ref parseContext);

                int dataStart = parseContext.Position;
                int dataEnd = FindInlineImageDataEnd(ref parseContext, dataStart);
                if (dataEnd < 0)
                {
                    _logger.LogWarning("Could not locate inline image EI sentinel – skipping image");
                    return;
                }

                int dataLength = dataEnd - dataStart;
                ReadOnlyMemory<byte> imageDataMemory = dataLength > 0
                    ? ExtractImageDataSlice(ref parseContext, dataStart, dataLength)
                    : ReadOnlyMemory<byte>.Empty;

                // Advance past EI
                parseContext.Position = dataEnd + 2;

                var inlineObject = new PdfObject(new PdfReference(-1), _page.Document, PdfValue.Dictionary(imageDictionary))
                {
                    StreamData = imageDataMemory
                };

                var pdfImage = PdfImage.FromXObject(inlineObject, _page, name: null, isSoftMask: false);
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
                if (string.IsNullOrEmpty(rawKey))
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

            if (!imageDictionary.HasKey(PdfTokens.BitsPerComponentKey) && !imageDictionary.GetBoolOrDefault(PdfTokens.ImageMaskKey))
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
            if (!imageDictionary.HasKey(PdfTokens.ColorSpaceKey) && !imageDictionary.GetBoolOrDefault(PdfTokens.ImageMaskKey))
            {
                imageDictionary.Set(PdfTokens.ColorSpaceKey, PdfValue.Name(PdfColorSpaceNames.DeviceGray));
            }
            if (imageDictionary.GetBoolOrDefault(PdfTokens.ImageMaskKey) && !imageDictionary.HasKey(PdfTokens.BitsPerComponentKey))
            {
                imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValue.Integer(1));
            }

            return imageDictionary;
        }

        private void SkipSingleWhitespaceAfterId(ref PdfParseContext parseContext)
        {
            if (parseContext.IsAtEnd)
            {
                return;
            }

            var next = parseContext.PeekByte();
            if (PdfParsingHelpers.IsWhitespace(next))
            {
                parseContext.Advance(1);
            }
        }

        private ReadOnlyMemory<byte> ExtractImageDataSlice(ref PdfParseContext parseContext, int dataStart, int dataLength)
        {
            if (parseContext.IsSingleMemory)
            {
                return parseContext.OriginalMemory.Slice(dataStart, dataLength);
            }

            var span = parseContext.GetSlice(dataStart, dataLength);
            return new ReadOnlyMemory<byte>(span.ToArray());
        }

        private int FindInlineImageDataEnd(ref PdfParseContext context, int start)
        {
            if (!context.IsSingleMemory)
            {
                return -1; // Multi-chunk fallback not implemented.
            }

            var span = context.OriginalMemory.Span;
            int length = span.Length;
            for (int index = start; index + 1 < length; index++)
            {
                if (span[index] == (byte)'E' && span[index + 1] == (byte)'I')
                {
                    bool precedingWhitespace = index - 1 >= start && PdfParsingHelpers.IsWhitespace(span[index - 1]);
                    byte following = index + 2 < length ? span[index + 2] : (byte)0;
                    bool followingDelimiter = index + 2 >= length || PdfParsingHelpers.IsWhitespace(following) || PdfParsingHelpers.IsDelimiter(following);
                    if (precedingWhitespace && followingDelimiter)
                    {
                        return index;
                    }
                }
            }
            return -1;
        }

        private IPdfValue NormalizeInlineImageValue(string expandedKey, IPdfValue value)
        {
            if (expandedKey == PdfTokens.ColorSpaceKey && value.Type == PdfValueType.Name)
            {
                var name = value.AsName();
                if (!string.IsNullOrEmpty(name))
                {
                    switch (name)
                    {
                        case "/G": return PdfValue.Name(PdfColorSpaceNames.DeviceGray);
                        case "/RGB": return PdfValue.Name(PdfColorSpaceNames.DeviceRGB);
                        case "/CMYK": return PdfValue.Name(PdfColorSpaceNames.DeviceCMYK);
                        case "/I": return PdfValue.Name(PdfColorSpaceNames.Indexed);
                    }
                }
            }
            else if (expandedKey == PdfTokens.FilterKey)
            {
                if (value.Type == PdfValueType.Name)
                {
                    var filterName = value.AsName();
                    if (!string.IsNullOrEmpty(filterName))
                    {
                        return PdfValue.Name(ExpandFilterName(filterName));
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
                                if (!string.IsNullOrEmpty(itemName))
                                {
                                    newValues.Add(PdfValue.Name(ExpandFilterName(itemName)));
                                    continue;
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

        private string ExpandInlineImageKey(string key)
        {
            switch (key)
            {
                case PdfTokens.WidthKey:
                case PdfTokens.HeightKey:
                case PdfTokens.BitsPerComponentKey:
                case PdfTokens.ColorSpaceKey:
                case PdfTokens.DecodeKey:
                case PdfTokens.DecodeParmsKey:
                case PdfTokens.FilterKey:
                case PdfTokens.ImageMaskKey:
                case PdfTokens.MaskKey:
                {
                    return key;
                }
            }

            switch (key)
            {
                case "/W": return PdfTokens.WidthKey;
                case "/H": return PdfTokens.HeightKey;
                case "/BPC": return PdfTokens.BitsPerComponentKey;
                case "/CS": return PdfTokens.ColorSpaceKey;
                case "/D": return PdfTokens.DecodeKey;
                case "/DP": return PdfTokens.DecodeParmsKey;
                case "/F": return PdfTokens.FilterKey;
                case "/IM": return PdfTokens.ImageMaskKey;
                default: return key;
            }
        }

        private string ExpandFilterName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            if (raw[0] != '/')
            {
                raw = "/" + raw;
            }

            var core = raw.Substring(1);
            switch (core)
            {
                case "Fl": return PdfTokens.FlateDecode;
                case "LZW": return PdfTokens.LZWDecode;
                case "AHx": return PdfTokens.ASCIIHexDecode;
                case "A85": return PdfTokens.ASCII85Decode;
                case "RL": return PdfTokens.RunLengthDecode;
                case "CCF": return PdfTokens.CCITTFaxDecode;
                case "DCT": return PdfTokens.DCTDecode;
                case "JPX": return PdfTokens.JPXDecode;
                case "JB2": return PdfTokens.JBIG2Decode;
                default: return raw;
            }
        }

        private void ProcessUnexpectedEndInlineImage()
        {
            _logger.LogWarning("Unexpected EI operator encountered standalone");
        }
    }
}
