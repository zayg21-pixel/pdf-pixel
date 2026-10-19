using PdfPixel.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Reads the next token of a content stream. Numbers, booleans, null and operators are read by value;
    /// every other value is parsed into <see cref="PdfParseContext.Values"/>, which is filled again from its first
    /// slot on the read that follows an operator. Once every slot is taken, further values replace the last one.
    /// </summary>
    /// <param name="value">The token read.</param>
    /// <returns>False at the end of the content stream.</returns>
    public bool TryReadContentStreamValue(out PdfContentStreamValue value)
    {
        if (_clearValuesOnNextRead)
        {
            _clearValuesOnNextRead = false;
            _valueCount = 0;
        }

        if (_inlineImagePending)
        {
            _inlineImagePending = false;

            PdfValue? inlineImage = ReadInlineImage();
            if (inlineImage != null)
            {
                value = AddValue(inlineImage);
                return true;
            }
        }

        while (true)
        {
            SkipWhitespacesAndComments();
            int start = Position;

            switch (ReadToken())
            {
                case PdfTokenType.Number:
                {
                    if (ReadRawNumber(out int integerValue, out float realValue))
                    {
                        value = new PdfContentStreamValue(PdfContentStreamValueKind.Integer, integerValue);
                    }
                    else
                    {
                        value = new PdfContentStreamValue(PdfContentStreamValueKind.Real, Unsafe.As<float, int>(ref realValue));
                    }

                    return true;
                }
                case PdfTokenType.Name:
                {
                    value = AddValue(ReadName());
                    return true;
                }
                case PdfTokenType.String:
                {
                    value = AddValue(ReadString());
                    return true;
                }
                case PdfTokenType.HexString:
                {
                    value = AddValue(ReadHexString());
                    return true;
                }
                case PdfTokenType.ArrayStart:
                case PdfTokenType.DictionaryStart:
                {
                    Position = start;
                    PdfValue? collection = ReadNextValue();

                    if (collection == null)
                    {
                        value = default;
                        return false;
                    }

                    value = AddValue(collection);
                    return true;
                }
                case PdfTokenType.Operator:
                {
                    value = ReadOperatorValue();

                    if (value.Kind == PdfContentStreamValueKind.Operator)
                    {
                        _clearValuesOnNextRead = true;
                        _inlineImagePending = (PdfOperator)value.Value == PdfOperator.BeginInlineImage;
                    }

                    return true;
                }
                case PdfTokenType.InlineStreamStart:
                {
                    // ID without BI: the image has no dictionary entries.
                    PdfInlineImageValue? inlineImage = ReadInlineStream();
                    if (inlineImage != null)
                    {
                        value = AddValue(inlineImage);
                        return true;
                    }

                    value = default;
                    return false;
                }
                case PdfTokenType.ContextEnd:
                {
                    value = default;
                    return false;
                }
            }
        }
    }

    private PdfContentStreamValue AddValue(PdfValue value)
    {
        PdfValue[] values = _parseContext.Values;
        int index = Math.Min(_valueCount, values.Length - 1);

        values[index] = value;
        _valueCount = index + 1;

        return new PdfContentStreamValue(PdfContentStreamValueKind.Value, index);
    }

    /// <summary>
    /// Reads an operator token the way <see cref="ReadOperator"/> does, as a boolean, null or operator token.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfContentStreamValue ReadOperatorValue()
    {
        ulong packedToken = 0;
        int length = 0;

        while (!IsAtEnd)
        {
            byte currentByte = PeekByte();
            if (IsTokenTerminator(currentByte))
            {
                break;
            }

            Advance(1);
            if (length < MaxKeywordLength)
            {
                packedToken |= (ulong)currentByte << (length * 8);
            }

            length++;
        }

        if (length <= MaxKeywordLength)
        {
            if (packedToken == TrueToken)
            {
                return new PdfContentStreamValue(PdfContentStreamValueKind.Boolean, 1);
            }
            else if (packedToken == FalseToken)
            {
                return new PdfContentStreamValue(PdfContentStreamValueKind.Boolean, 0);
            }
            else if (packedToken == NullToken)
            {
                return new PdfContentStreamValue(PdfContentStreamValueKind.Null, 0);
            }
        }

        PdfOperator contentOperator = (length <= MaxOperatorLength) ? (PdfOperator)packedToken : PdfOperator.Unknown;

        return new PdfContentStreamValue(PdfContentStreamValueKind.Operator, (int)contentOperator);
    }
}
