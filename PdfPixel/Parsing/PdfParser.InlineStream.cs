using PdfPixel.Color.ColorSpace;
using PdfPixel.Objects;
using PdfPixel.Rendering.Operators;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Reads an inline image: the dictionary entries that follow BI, the ID keyword and the image data.
    /// </summary>
    /// <returns>
    /// The inline image, or null when an operator or the end of the data comes before the image data ends;
    /// the position is then left where reading started.
    /// </returns>
    public PdfValue? ReadInlineImage()
    {
        int startPosition = Position;
        _inlineImageParameters.Clear();
        PdfValue? value;

        while ((value = ReadNextValue()) != null)
        {
            if (value is PdfInlineImageValue)
            {
                return value;
            }

            if (value is PdfOperatorValue)
            {
                break;
            }

            _inlineImageParameters.Add(value);
        }

        _inlineImageParameters.Clear();
        Position = startPosition;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfInlineImageValue? ReadInlineStream()
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

        int dataStart = Position;

        PdfDictionary imageDictionary = BuildImageDictionary();
        List<PdfFilterType> filters = PdfStreamDecoder.GetFilters(imageDictionary);
        PdfFilterType? filter = (filters.Count > 0) ? filters[0] : null;

        int dataEnd;
        bool endFound = filter switch
        {
            PdfFilterType.ASCII85Decode => TryReadUntilAscii85Eod(out dataEnd),
            PdfFilterType.ASCIIHexDecode => TryReadUntilAsciiHexEod(out dataEnd),
            PdfFilterType.DCTDecode => TryReadUntilJpegEoi(out dataEnd),
            null => TryReadRawImageData(imageDictionary, out dataEnd) || TryScanEi(out dataEnd),
            _ => TryScanEi(out dataEnd)
        };

        if (!endFound)
        {
            Position = beginPosition;
            return null;
        }

        PdfObject inlineObject = new(_document, new PdfDictionaryValue(imageDictionary), _parseContext.Slice(dataStart, dataEnd - dataStart).ToArray());

        return new PdfInlineImageValue(inlineObject);
    }

    /// <summary>
    /// Builds the fully expanded inline image dictionary from the BI/ID entries collected by
    /// <see cref="ReadInlineImage"/>, applying default values for /BitsPerComponent and /ColorSpace.
    /// The entries are consumed and cleared since they are not needed again once the dictionary is built.
    /// </summary>
    private PdfDictionary BuildImageDictionary()
    {
        PdfDictionary imageDictionary = new(_document);
        imageDictionary.Set(PdfTokens.SubtypeKey, new PdfNameValue(PdfTokens.ImageSubtype));

        List<PdfValue> parameters = _inlineImageParameters;

        for (int parameterIndex = 0; parameterIndex + 1 < parameters.Count; parameterIndex += 2)
        {
            PdfValue keyValue = parameters[parameterIndex];
            PdfString? rawKey = keyValue.AsName();
            if (rawKey == null)
            {
                break;
            }

            PdfString expandedKey = ExpandInlineImageKey(rawKey.Value);
            if (!imageDictionary.HasKey(expandedKey))
            {
                imageDictionary.Set(expandedKey, parameters[parameterIndex + 1]);
            }
        }

        parameters.Clear();

        if (!imageDictionary.HasKey(PdfTokens.BitsPerComponentKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
        {
            imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfIntegerValue.FromInteger(8));
        }

        if (!imageDictionary.HasKey(PdfTokens.ColorSpaceKey) && !imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey))
        {
            imageDictionary.Set(PdfTokens.ColorSpaceKey, new PdfNameValue(PdfColorSpaceType.DeviceGray.AsPdfString()));
        }

        if (imageDictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey) && !imageDictionary.HasKey(PdfTokens.BitsPerComponentKey))
        {
            imageDictionary.Set(PdfTokens.BitsPerComponentKey, PdfIntegerValue.One);
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

            case PdfInlineImageProperty.Interpolate:
                return PdfTokens.InterpolateKey;

            default:
                return key;
        }
    }

    private bool TryReadUntilAscii85Eod(out int dataEnd)
    {
        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current != (byte)'~')
            {
                continue;
            }

            while (!IsAtEnd && IsWhitespace(PeekByte()))
            {
                Advance(1);
            }

            if (!IsAtEnd && PeekByte() == (byte)'>')
            {
                Advance(1);
            }

            dataEnd = Position;
            return SkipToEiOperator();
        }

        dataEnd = Position;
        return false;
    }

    private bool TryReadUntilAsciiHexEod(out int dataEnd)
    {
        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current == (byte)'>')
            {
                dataEnd = Position;
                return SkipToEiOperator();
            }
        }

        dataEnd = Position;
        return false;
    }

    /// <summary>
    /// Walks JPEG markers to find the EOI marker (0xFFD9) instead of scanning raw bytes for it, since
    /// length-prefixed segments such as APPn (which may embed a full nested JPEG thumbnail) can otherwise
    /// contain a coincidental 0xFFD9 byte pair that is not the real end of the image.
    /// </summary>
    private bool TryReadUntilJpegEoi(out int dataEnd)
    {
        dataEnd = Position;

        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current != (byte)0xFF || IsAtEnd)
            {
                continue;
            }

            byte marker = PeekByte();

            if (marker == 0x00)
            {
                // Byte-stuffed literal 0xFF within entropy-coded scan data, not a marker.
                Advance(1);
                continue;
            }

            if (marker == (byte)0xFF)
            {
                // Fill byte padding before the real marker; re-check it as the next prefix candidate.
                continue;
            }

            if (marker == (byte)0xD9)
            {
                Advance(1);
                dataEnd = Position;
                return SkipToEiOperator();
            }

            if ((marker >= 0xD0 && marker <= 0xD8) || marker == 0x01)
            {
                // Standalone marker (RST0-7, SOI, TEM): no length-prefixed payload follows.
                Advance(1);
                continue;
            }

            Advance(1);

            if (Position + 1 >= Length)
            {
                return false;
            }

            int segmentLength = (PeekByte() << 8) | PeekByte(1);
            if (segmentLength < 2)
            {
                return false;
            }

            if (Position + segmentLength > Length)
            {
                return false;
            }

            Advance(segmentLength);
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
    private bool TryReadRawImageData(PdfDictionary imageDictionary, out int dataEnd)
    {
        int dataStart = Position;
        dataEnd = dataStart;

        int? dataLength = ComputeRawDataLength(imageDictionary);
        if (dataLength == null)
        {
            return false;
        }

        if (dataStart + dataLength.Value > Length)
        {
            return false;
        }

        Advance(dataLength.Value);
        dataEnd = Position;

        if (!SkipToEiOperator())
        {
            SetPosition(dataStart);
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

    private static int? ResolveInlineComponentCount(PdfValue? colorSpaceValue)
    {
        PdfString? colorSpaceName = colorSpaceValue.AsName();
        if (colorSpaceName == null)
        {
            return null;
        }

        switch (colorSpaceName.Value.AsEnum<PdfColorSpaceType>())
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

    private bool TryScanEi(out int dataEnd)
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
                        dataEnd = Position;
                        return true;
                    }
                }
            }

            previousByte = current;
        }

        dataEnd = Position;
        return false;
    }
}
