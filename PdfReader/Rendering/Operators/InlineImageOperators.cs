using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.Image;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Operators
{
    internal class InlineImageOperators
    {
        /// <summary>
        /// Process inline image related operators (BI / ID / EI)
        /// </summary>
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, ref PdfParseContext parseContext, PdfGraphicsState graphicsState,
                                         SKCanvas canvas, PdfPage page)
        {
            switch (op)
            {
                case "BI": // Begin inline image (parameters follow until ID)
                    ProcessBeginInlineImage(operandStack);
                    return true;
                case "ID": // Inline image data (operand stack now has parameters collected since BI)
                    ProcessInlineImageData(operandStack, ref parseContext, graphicsState, canvas, page);
                    return true;
                case "EI": // Should never be directly processed (we consume it while reading data)
                    ProcessEndInlineImage(operandStack);
                    return true;
                default:
                    return false;
            }
        }

        private static void ProcessBeginInlineImage(Stack<IPdfValue> operandStack)
        {
            // Marker only – parameters accumulate on operand stack until ID
        }

        /// <summary>
        /// Parse inline image parameters from operand stack, read raw image bytes until EI sentinel, then render.
        /// </summary>
        private static void ProcessInlineImageData(Stack<IPdfValue> operandStack, ref PdfParseContext parseContext,
                                                   PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            try
            {
                // 1. Collect parameters (stack bottom -> top is original order; we need FIFO for key/value pairs)
                var paramValues = new List<IPdfValue>(operandStack);
                paramValues.Reverse(); // Now in reading order
                operandStack.Clear();

                // 2. Build dictionary with abbreviation expansion
                var imageDict = new PdfDictionary(page.Document);
                imageDict.Set(PdfTokens.SubtypeKey, PdfValue.Name(PdfTokens.ImageSubtype));

                for (int i = 0; i + 1 < paramValues.Count; i += 2)
                {
                    var keyVal = paramValues[i];
                    var valueVal = paramValues[i + 1];
                    if (keyVal.Type != PdfValueType.Name)
                    {
                        // Not a name – malformed; stop to avoid misalignment
                        break;
                    }

                    var rawKey = keyVal.AsName();
                    if (string.IsNullOrEmpty(rawKey))
                    {
                        continue;
                    }

                    // Ensure starts with '/'
                    if (rawKey[0] != '/') rawKey = "/" + rawKey;

                    var expandedKey = ExpandInlineImageKey(rawKey);
                    var normalizedValue = NormalizeInlineImageValue(expandedKey, valueVal, page);

                    // If key already exists do not overwrite (later duplicates ignored)
                    if (!imageDict.HasKey(expandedKey))
                    {
                        imageDict.Set(expandedKey, normalizedValue ?? valueVal);
                    }
                }

                // 3. Supply defaults if missing
                if (!imageDict.HasKey(PdfTokens.BitsPerComponentKey) && !imageDict.GetBoolOrDefault(PdfTokens.ImageMaskKey))
                {
                    imageDict.Set(PdfTokens.BitsPerComponentKey, PdfValue.Integer(8));
                }
                if (!imageDict.HasKey(PdfTokens.WidthKey))
                {
                    Console.WriteLine("Inline image missing /Width – skipping");
                    return;
                }
                if (!imageDict.HasKey(PdfTokens.HeightKey))
                {
                    Console.WriteLine("Inline image missing /Height – skipping");
                    return;
                }
                if (!imageDict.HasKey(PdfTokens.ColorSpaceKey) && !imageDict.GetBoolOrDefault(PdfTokens.ImageMaskKey))
                {
                    // Default per spec is DeviceGray
                    imageDict.Set(PdfTokens.ColorSpaceKey, PdfValue.Name("/DeviceGray"));
                }
                if (imageDict.GetBoolOrDefault(PdfTokens.ImageMaskKey) && !imageDict.HasKey(PdfTokens.BitsPerComponentKey))
                {
                    imageDict.Set(PdfTokens.BitsPerComponentKey, PdfValue.Integer(1));
                }

                // 4. Skip single whitespace char after ID (if present)
                if (!parseContext.IsAtEnd)
                {
                    var b = parseContext.PeekByte();
                    if (PdfParsingHelpers.IsWhitespace(b))
                    {
                        parseContext.Advance(1);
                    }
                }

                int dataStart = parseContext.Position;
                int dataEnd = FindInlineImageDataEnd(ref parseContext, dataStart);
                if (dataEnd < 0)
                {
                    Console.WriteLine("Could not locate inline image EI sentinel – skipping image");
                    return;
                }

                int dataLength = dataEnd - dataStart;
                ReadOnlyMemory<byte> imageDataMem;
                if (dataLength > 0)
                {
                    if (parseContext.IsSingleMemory)
                    {
                        imageDataMem = parseContext.OriginalMemory.Slice(dataStart, dataLength);
                    }
                    else
                    {
                        // Fallback allocation (rare path)
                        var span = parseContext.GetSlice(dataStart, dataLength);
                        imageDataMem = new ReadOnlyMemory<byte>(span.ToArray());
                    }
                }
                else
                {
                    imageDataMem = ReadOnlyMemory<byte>.Empty;
                }

                // Advance past EI
                parseContext.Position = dataEnd + 2; // Skip 'E''I'

                // 5. Construct synthetic PdfObject
                var inlineObj = new PdfObject(new PdfReference(-1), page.Document, PdfValue.Dictionary(imageDict))
                {
                    StreamData = imageDataMem
                };

                // 6. Decode & render via existing pipeline
                var pdfImage = PdfImage.FromXObject(inlineObj, page, name: null, isSoftMask: false);

                page.Document.PdfRenderer.DrawUnitImage(canvas, pdfImage, graphicsState, page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing inline image: {ex.Message}");
            }
        }

        /// <summary>
        /// Locate the end of inline image data ensuring the EI sentinel is delimited.
        /// Returns position of 'E' in EI or -1 if not found.
        /// </summary>
        private static int FindInlineImageDataEnd(ref PdfParseContext context, int start)
        {
            // Only implemented for single memory fast path. Multi-chunk fallback not yet implemented.
            if (!context.IsSingleMemory)
            {
                // TODO: implement multi-chunk scan (rare)
                return -1;
            }

            var span = context.OriginalMemory.Span;
            int length = span.Length;
            for (int i = start; i + 1 < length; i++)
            {
                if (span[i] == (byte)'E' && span[i + 1] == (byte)'I')
                {
                    // Preceding must be whitespace (spec) and following delimiter
                    bool precedingWs = i - 1 >= start && PdfParsingHelpers.IsWhitespace(span[i - 1]);
                    byte following = i + 2 < length ? span[i + 2] : (byte)0; // EOF acceptable
                    bool followingDelim = i + 2 >= length || PdfParsingHelpers.IsWhitespace(following) || PdfParsingHelpers.IsDelimiter(following);
                    if (precedingWs && followingDelim)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private static IPdfValue NormalizeInlineImageValue(string expandedKey, IPdfValue value, PdfPage page)
        {
            if (expandedKey == PdfTokens.ColorSpaceKey && value.Type == PdfValueType.Name)
            {
                string name = value.AsName();
                if (!string.IsNullOrEmpty(name))
                {
                    switch (name)
                    {
                        case "/G": return PdfValue.Name("/DeviceGray");
                        case "/RGB": return PdfValue.Name("/DeviceRGB");
                        case "/CMYK": return PdfValue.Name("/DeviceCMYK");
                        case "/I": return PdfValue.Name("/Indexed");
                    }
                }
            }
            else if (expandedKey == PdfTokens.FilterKey)
            {
                // Normalize filter name(s) (single or array)
                if (value.Type == PdfValueType.Name)
                {
                    string filterName = value.AsName();
                    if (!string.IsNullOrEmpty(filterName))
                    {
                        return PdfValue.Name(ExpandFilterName(filterName));
                    }
                }
                else if (value.Type == PdfValueType.Array)
                {
                    var arr = value.AsArray();
                    if (arr != null && arr.Count > 0)
                    {
                        var newArr = new List<IPdfValue>(arr.Count);
                        for (int i = 0; i < arr.Count; i++)
                        {
                            var item = arr.GetValue(i);
                            if (item != null && item.Type == PdfValueType.Name)
                            {
                                string fname = item.AsName();
                                if (!string.IsNullOrEmpty(fname))
                                {
                                    newArr.Add(PdfValue.Name(ExpandFilterName(fname)));
                                    continue;
                                }
                            }
                            newArr.Add(item);
                        }
                        return PdfValue.Array(new PdfArray(arr.Document, newArr));
                    }
                }
            }
            return value;
        }

        private static string ExpandInlineImageKey(string key)
        {
            // Already full keys
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
                    return key;
            }

            // Abbreviations per spec
            return key switch
            {
                "/W" => PdfTokens.WidthKey,
                "/H" => PdfTokens.HeightKey,
                "/BPC" => PdfTokens.BitsPerComponentKey,
                "/CS" => PdfTokens.ColorSpaceKey,
                "/D" => PdfTokens.DecodeKey,
                "/DP" => PdfTokens.DecodeParmsKey,
                "/F" => PdfTokens.FilterKey,
                "/IM" => PdfTokens.ImageMaskKey,
                _ => key
            };
        }

        private static string ExpandFilterName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }
            if (raw[0] != '/')
            {
                raw = "/" + raw;
            }
            // Remove leading slash for switch logic
            var core = raw.Substring(1);
            switch (core)
            {
                case "Fl": return PdfTokens.FlateDecode; // /Fl -> /FlateDecode
                case "LZW": return PdfTokens.LZWDecode;
                case "AHx": return PdfTokens.ASCIIHexDecode;
                case "A85": return PdfTokens.ASCII85Decode;
                case "RL": return PdfTokens.RunLengthDecode;
                case "CCF": return PdfTokens.CCITTFaxDecode;
                case "DCT": return PdfTokens.DCTDecode;
                case "JPX": return PdfTokens.JPXDecode;
                case "JB2": return PdfTokens.JBIG2Decode;
                default:
                    return raw; // Already full or unrecognized
            }
        }

        private static void ProcessEndInlineImage(Stack<IPdfValue> operandStack)
        {
            // Should be handled inside ProcessInlineImageData – reaching here indicates parsing drift
            Console.WriteLine("Unexpected EI operator encountered standalone");
        }
    }
}
