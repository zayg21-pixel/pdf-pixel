using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Reads the next token of a content stream. Numbers, booleans, null and operators are read by value; names, strings,
    /// arrays and dictionaries are only measured and held as their extent, for <see cref="ReadContentValue"/> to parse when an
    /// operator needs them. The data that follows an ID operator is left for the operator, which reads it from the context
    /// and sets <see cref="PdfContentStreamCursor.SkipPositions"/> to the bytes it read; they are skipped before the next token.
    /// </summary>
    /// <param name="value">The token read.</param>
    /// <returns>False at the end of the content stream.</returns>
    public bool TryReadContentStreamValue(out PdfContentStreamValue value)
    {
        PdfContentStreamCursor? cursor = _parseContext.Cursor;

        if (cursor != null && cursor.SkipPositions > 0)
        {
            Advance(cursor.SkipPositions);
            cursor.SkipPositions = 0;
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
                    SkipRegularCharacters();
                    value = new PdfContentStreamValue(PdfContentStreamValueKind.Value, start, Position - start);
                    return true;
                }
                case PdfTokenType.String:
                {
                    SkipString();
                    value = new PdfContentStreamValue(PdfContentStreamValueKind.Value, start, Position - start);
                    return true;
                }
                case PdfTokenType.HexString:
                {
                    SkipHexString();
                    value = new PdfContentStreamValue(PdfContentStreamValueKind.Value, start, Position - start);
                    return true;
                }
                case PdfTokenType.ArrayStart:
                case PdfTokenType.DictionaryStart:
                {
                    SkipCollection();
                    value = new PdfContentStreamValue(PdfContentStreamValueKind.Value, start, Position - start);
                    return true;
                }
                case PdfTokenType.Operator:
                {
                    value = ReadOperatorValue();

                    if (cursor != null && value.Kind == PdfContentStreamValueKind.Operator)
                    {
                        switch ((PdfOperator)value.Value)
                        {
                            case PdfOperator.BeginInlineImage:
                            {
                                cursor.InlineImageStart = Position;
                                break;
                            }
                            case PdfOperator.EndInlineImage:
                            {
                                cursor.InlineImageStart = -1;
                                break;
                            }
                        }
                    }

                    return true;
                }
                case PdfTokenType.InlineStreamStart:
                {
                    if (cursor != null)
                    {
                        // ID without BI: the image has no dictionary entries.
                        if (cursor.InlineImageStart < 0)
                        {
                            cursor.InlineImageStart = start;
                        }

                        cursor.InlineImageDataStart = Position;
                    }

                    value = new PdfContentStreamValue(PdfContentStreamValueKind.Operator, (int)PdfOperator.InlineImageData);
                    return true;
                }
                case PdfTokenType.ContextEnd:
                {
                    value = default;
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Parses the name, string, array or dictionary of the content stream <paramref name="context"/> reads
    /// in the <paramref name="length"/> bytes at <paramref name="start"/>.
    /// </summary>
    /// <returns>The value, or null when the bytes hold none.</returns>
    public static PdfValue? ReadContentValue(PdfParseContext context, int start, int length)
    {
        IPdfDocumentInternal document = context.Document
            ?? throw new InvalidOperationException("Content stream values are parsed only from a content stream context.");
        ReadOnlySpan<byte> data = context.Slice(start, length);

        // Names, the most common operands, need no parser unless they hold #-escapes.
        if (data.Length > 0 && data[0] == ForwardSlash && data.IndexOf((byte)'#') < 0)
        {
            return new PdfNameValue(new PdfString(data.Slice(1).ToArray()));
        }

        PdfParser parser = new(new PdfParseContext(data, document), document, PdfParserMode.ContentStream);

        return parser.ReadNextValue();
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
