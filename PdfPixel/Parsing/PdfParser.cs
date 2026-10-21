using PdfPixel.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

#pragma warning disable RCS1019 // Order modifiers
internal ref partial struct PdfParser
#pragma warning restore RCS1019 // Order modifiers
{
    // Literal tokens packed little-endian
    private const ulong TrueToken = 't' | ('r' << 8) | ('u' << 16) | ((ulong)'e' << 24);
    private const ulong FalseToken = 'f' | ('a' << 8) | ('l' << 16) | ((ulong)'s' << 24) | ((ulong)'e' << 32);
    private const ulong NullToken = 'n' | ('u' << 8) | ('l' << 16) | ((ulong)'l' << 24);

    private const int MaxOperatorLength = 3;
    private const int MaxKeywordLength = sizeof(ulong);

    private readonly ReadOnlySpan<double> inversePowersOf10 =
    [
        1.0,
        0.1,
        0.01,
        0.001,
        0.0001,
        0.00001,
        0.000001,
        0.0000001,
        0.00000001,
        0.000000001
    ];

    private readonly List<byte> _localBuffer = [];
    private readonly List<PdfValue> _values = [];
    private readonly List<PdfValue> _inlineImageParameters = [];
    private readonly Stack<CollectionFrame> _frames = [];
    private readonly BufferedStream? _stream;
    private readonly bool _streamMode;
    private readonly IPdfDocumentInternal _document;
    private readonly int _length;
    private readonly bool _useOperators;
    private readonly bool _allowReferences;
    private readonly bool _decrypt;

    private int _lastSetPostion = 0;
    private bool _inlineImagePending;
    private bool _clearValuesOnNextRead;
    private int _valueCount;
    private PdfParseContext _parseContext;
    private PdfReference? _currentReference;

    public PdfParser(PdfParseContext parseContext, IPdfDocumentInternal document, PdfParserMode mode)
    {
        _parseContext = parseContext;
        _document = document;
        _useOperators = mode == PdfParserMode.ContentStream;
        _allowReferences = mode != PdfParserMode.ContentStream;
        _decrypt = mode == PdfParserMode.IndirectObject;
        _length = parseContext.Length;
    }

    public PdfParser(BufferedStream bufferedStream, IPdfDocumentInternal document, PdfParserMode mode)
    {
        _stream = bufferedStream;
        _length = (int)bufferedStream.Length;
        _document = document;
        _useOperators = mode == PdfParserMode.ContentStream;
        _allowReferences = mode != PdfParserMode.ContentStream;
        _decrypt = mode == PdfParserMode.IndirectObject;
        _streamMode = true;
    }

    /// <summary>
    /// Current absolute byte position within the underlying parse context.
    /// Setting this advances or rewinds the parser to a new location (bounds clamped to context length).
    /// </summary>
    public int Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetPosition();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetPosition(value);
    }

    /// <summary>
    /// Total length of the underlying parse context in bytes.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// True if the parser has reached the end of the underlying parse context.
    /// </summary>
    public bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetIsAtEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfValue? ReadNextValue()
    {
        List<PdfValue> values = _values;
        Stack<CollectionFrame> frames = _frames;
        values.Clear();
        frames.Clear();

        while (!IsAtEnd)
        {
            // Skip leading whitespace/comments before each token.
            SkipWhitespacesAndComments();

            PdfTokenType token = ReadToken();
            switch (token)
            {
                case PdfTokenType.HexString:
                {
                    PdfValue hexValue = ReadHexString();
                    values.Add(hexValue);
                    break;
                }
                case PdfTokenType.String:
                {
                    PdfValue strValue = ReadString();
                    values.Add(strValue);
                    break;
                }
                case PdfTokenType.Name:
                {
                    PdfValue nameValue = ReadName();
                    values.Add(nameValue);
                    break;
                }
                case PdfTokenType.Operator:
                {
                    PdfValue opValue = ReadOperator();
                    values.Add(opValue);
                    break;
                }
                case PdfTokenType.Number:
                {
                    PdfValue numberValue = ReadNumber();
                    values.Add(numberValue);
                    break;
                }
                case PdfTokenType.ArrayStart:
                {
                    frames.Push(new CollectionFrame(PdfTokenType.ArrayStart, values.Count));
                    break;
                }
                case PdfTokenType.ArrayEnd:
                {
                    HandleArrayEnd(values, frames);
                    break;
                }
                case PdfTokenType.DictionaryStart:
                {
                    frames.Push(new CollectionFrame(PdfTokenType.DictionaryStart, values.Count));
                    break;
                }
                case PdfTokenType.DictionaryEnd:
                {
                    HandleDictionaryEnd(values, frames);
                    break;
                }
                case PdfTokenType.Reference:
                {
                    if (values.Count >= 2)
                    {
                        int? generation = values[values.Count - 1].AsInteger();
                        int? objectNumber = values[values.Count - 2].AsInteger();

                        // Without two numeric operands this is not a reference; leave the values in place.
                        if (generation != null && objectNumber != null)
                        {
                            values.RemoveRange(values.Count - 2, 2);

                            // Invalid object identifier (7.3.10): the null object.
                            if (objectNumber.Value > 0 && generation.Value >= 0)
                            {
                                values.Add(new PdfReferenceValue(new PdfReference((uint)objectNumber.Value, generation.Value)));
                            }
                            else
                            {
                                values.Add(PdfNullValue.Instance);
                            }
                        }
                    }

                    break;
                }
                case PdfTokenType.InlineStreamStart:
                {
                    PdfValue? streamValue = ReadInlineStream();
                    if (streamValue == null)
                    {
                        return null;
                    }

                    values.Add(streamValue);
                    break;
                }
            }

            if (frames.Count == 0 && values.Count > 0)
            {
                return values[values.Count - 1];
            }
        }

        // End of parsing context. Return last fully materialized top-level value if present.
        if (frames.Count == 0 && values.Count > 0)
        {
            return values[values.Count - 1];
        }

        return null;
    }

    // Frame describing an open collection (array or dictionary) with start index in the value list.
    // Stores the originating opening token type so we can validate matching closing tokens.
    private struct CollectionFrame
    {
        public PdfTokenType FrameToken;
        public int StartIndex;

        public CollectionFrame(PdfTokenType frameToken, int startIndex)
        {
            FrameToken = frameToken;
            StartIndex = startIndex;
        }
    }
}
