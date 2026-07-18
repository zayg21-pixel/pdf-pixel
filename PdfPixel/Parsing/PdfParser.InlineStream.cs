using PdfPixel.Color.ColorSpace;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Streams;
using PdfPixel.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue? ReadInlineStream(Stack<IPdfValue>? operandStack)
    {
        // Skip single whitespace after ID per PDF spec.
        int beginPosition = Position;
        byte firstByte = PeekByte();
        if (firstByte == CarriageReturn)
        {
            Advance(1);
            if (!IsAtEnd && PeekByte() == LineFeed)
            {
                Advance(1);
            }
        }
        else if (IsWhitespace(firstByte))
        {
            Advance(1);
        }

        _localBuffer.Clear();

        PdfDictionary imageDictionary = BuildImageDictionary(operandStack);
        List<PdfFilterType> filters = PdfStreamDecoder.GetFilters(imageDictionary);
        PdfFilterType? filter = (filters.Count > 0) ? filters[0] : null;

        bool endFound = filter switch
        {
            PdfFilterType.ASCII85Decode => TryReadUntilAscii85Eod(),
            PdfFilterType.ASCIIHexDecode => TryReadUntilAsciiHexEod(),
            null => TryReadRawImageData(imageDictionary) || TryScanEi(),
            _ => TryScanEi()
        };

        if (!endFound)
        {
            Position = beginPosition;
            return null;
        }

        PdfObject inlineObject = new(default, _document, PdfValueFactory.Dictionary(imageDictionary)) { EmbaddedStream = new PdfString([.. _localBuffer]).Value };

        return PdfValueFactory.InlineImage(inlineObject);
    }

    /// <summary>
    /// Builds the fully expanded inline image dictionary from the BI/ID operand pairs accumulated on
    /// <paramref name="operandStack"/>, applying default values for /BitsPerComponent and /ColorSpace.
    /// The stack is consumed and cleared since the pairs are not needed again once the dictionary is built.
    /// </summary>
    private PdfDictionary BuildImageDictionary(Stack<IPdfValue>? operandStack)
    {
        PdfDictionary imageDictionary = new(_document);
        imageDictionary.Set(PdfTokens.SubtypeKey, PdfValueFactory.Name(PdfTokens.ImageSubtype));

        if (operandStack != null && operandStack.Count > 0)
        {
            List<IPdfValue> parameters = new(operandStack);
            parameters.Reverse();
            operandStack.Clear();

            for (int parameterIndex = 0; parameterIndex + 1 < parameters.Count; parameterIndex += 2)
            {
                IPdfValue keyValue = parameters[parameterIndex];
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
                    imageDictionary.Set(expandedKey, parameters[parameterIndex + 1]);
                }
            }
        }

        if (!imageDictionary.HasKey(PdfTokens.BitsPerComponentKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
        {
            imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfValueFactory.Integer(8));
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
    private static PdfString ExpandInlineImageKey(in PdfString key)
    {
        switch (key.AsEnum<PdfInlineImageProperty>())
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

    private bool TryReadUntilAscii85Eod()
    {
        while (!IsAtEnd)
        {
            byte current = ReadByte();
            _localBuffer.Add(current);

            if (current == (byte)'~' && !IsAtEnd && PeekByte() == (byte)'>')
            {
                Advance(1);
                _localBuffer.Add((byte)'>');
                return SkipToEiOperator();
            }
        }

        return false;
    }

    private bool TryReadUntilAsciiHexEod()
    {
        while (!IsAtEnd)
        {
            byte current = ReadByte();
            _localBuffer.Add(current);

            if (current == (byte)'>')
            {
                return SkipToEiOperator();
            }
        }

        return false;
    }

    private bool SkipToEiOperator()
    {
        while (!IsAtEnd && IsWhitespace(PeekByte()))
        {
            Advance(1);
        }

        if (IsAtEnd || PeekByte() != (byte)'E' || Position + 1 >= Length || PeekByte(1) != (byte)'I')
        {
            return false;
        }

        byte following = (Position + 2 < Length) ? PeekByte(2) : (byte)0;
        return (Position + 2 >= Length) || IsTokenTerminator(following);
    }

    /// <summary>
    /// Reads exactly the number of data bytes computed from /Width, /Height, /BitsPerComponent and /ColorSpace
    /// (or /ImageMask) when the image is unfiltered. Unfiltered inline image data is arbitrary binary and may
    /// coincidentally contain a whitespace-delimited "EI" sequence, so the length must be computed instead of
    /// found by scanning.
    /// </summary>
    private bool TryReadRawImageData(PdfDictionary imageDictionary)
    {
        int? dataLength = ComputeRawDataLength(imageDictionary);
        if (dataLength == null)
        {
            return false;
        }

        int dataStart = Position;
        if (dataStart + dataLength.Value > Length)
        {
            return false;
        }

        for (int index = 0; index < dataLength.Value; index++)
        {
            _localBuffer.Add(ReadByte());
        }

        if (!SkipToEiOperator())
        {
            SetPosition(dataStart);
            _localBuffer.Clear();
            return false;
        }

        return true;
    }

    private static int? ComputeRawDataLength(PdfDictionary imageDictionary)
    {
        if (!imageDictionary.HasKey(PdfTokens.WidthKey) || !imageDictionary.HasKey(PdfTokens.HeightKey))
        {
            return null;
        }

        int width = imageDictionary.GetIntegerOrDefault(PdfTokens.WidthKey);
        int height = imageDictionary.GetIntegerOrDefault(PdfTokens.HeightKey);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        bool imageMask = imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey);
        int bitsPerComponent = imageMask ? 1 : imageDictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey);
        int? components = imageMask ? 1 : ResolveInlineComponentCount(imageDictionary.GetValue(PdfTokens.ColorSpaceKey));

        if (bitsPerComponent <= 0 || components == null || components.Value <= 0)
        {
            return null;
        }

        long bitsPerRow = (long)width * bitsPerComponent * components.Value;
        long rowBytes = (bitsPerRow + 7) / 8;
        long totalBytes = rowBytes * height;

        return (totalBytes > 0 && totalBytes <= int.MaxValue) ? (int)totalBytes : null;
    }

    private static int? ResolveInlineComponentCount(IPdfValue? colorSpaceValue)
    {
        if (colorSpaceValue == null || colorSpaceValue.Type != PdfValueType.Name)
        {
            return null;
        }

        switch (colorSpaceValue.AsName().AsEnum<PdfColorSpaceType>())
        {
            case PdfColorSpaceType.DeviceGray:
                return 1;

            case PdfColorSpaceType.DeviceRGB:
                return 3;

            case PdfColorSpaceType.DeviceCMYK:
                return 4;

            case PdfColorSpaceType.Indexed:
                return 1;

            default:
                return null;
        }
    }

    private bool TryScanEi()
    {
        int previousByte = -1;

        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current == (byte)'E' && !IsAtEnd)
            {
                byte next = PeekByte();
                if (next == (byte)'I')
                {
                    bool precedingWhitespace = previousByte == -1 || IsWhitespace((byte)previousByte);
                    byte following = (Position + 1 < Length) ? PeekByte(1) : (byte)0;
                    bool followingDelimiter = (Position + 1 >= Length) || IsTokenTerminator(following);

                    if (precedingWhitespace && followingDelimiter)
                    {
                        SetPosition(Position - 1);
                        return true;
                    }
                }
            }

            _localBuffer.Add(current);
            previousByte = current;
        }

        return false;
    }
}
