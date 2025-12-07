using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing;

internal ref partial struct PdfParser
{
    // Boolean literal constants
    private readonly ReadOnlySpan<byte> TrueValue = "true"u8;
    private readonly ReadOnlySpan<byte> FalseValue = "false"u8;
    private readonly ReadOnlySpan<byte> NullValue = "null"u8;

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

    private int _lastSetPostion = 0;
    private readonly List<byte> _localBuffer = new List<byte>();
    private readonly BufferedStream _stream;
    private readonly bool _streamMode;
    private readonly PdfDocument _document;
    private readonly int _length;
    private readonly bool _allowReferences;
    private readonly bool _decrypt;
    private PdfParseContext _parseContext;
    private PdfReference _currentReference;

    public PdfParser(PdfParseContext parseContext, PdfDocument document, bool allowReferences, bool decrypt)
    {
        _parseContext = parseContext;
        _document = document;
        _allowReferences = allowReferences;
        _decrypt = decrypt;
        _length = parseContext.Length;
    }

    public PdfParser(BufferedStream bufferedStream, PdfDocument document, bool allowReferences, bool decrypt)
    {
        _stream = bufferedStream;
        _length = (int)bufferedStream.Length;
        _document = document;
        _allowReferences = allowReferences;
        _decrypt = decrypt;
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
    public IPdfValue ReadNextValue()
    {
        List<IPdfValue> values = new List<IPdfValue>();
        Stack<CollectionFrame> frames = new Stack<CollectionFrame>();

        while (!IsAtEnd)
        {
            // Skip leading whitespace/comments before each token.
            SkipWhitespacesAndComments();

            PdfTokenType token = ReadToken();
            switch (token)
            {
                case PdfTokenType.HexString:
                {
                    IPdfValue hexValue = ReadHexString();
                    values.Add(hexValue);
                    break;
                }
                case PdfTokenType.String:
                {
                    IPdfValue strValue = ReadString();
                    values.Add(strValue);
                    break;
                }
                case PdfTokenType.Name:
                {
                    IPdfValue nameValue = ReadName();
                    values.Add(nameValue);
                    break;
                }
                case PdfTokenType.Operator:
                {
                    IPdfValue opValue = ReadOperator();
                    values.Add(opValue);
                    break;
                }
                case PdfTokenType.Number:
                {
                    IPdfValue numberValue = ReadNumber();
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
                        int generation = values[values.Count - 1].AsInteger();
                        uint objectNumber = (uint)values[values.Count - 2].AsInteger();
                        values.RemoveRange(values.Count - 2, 2);
                        values.Add(PdfValueFactory.Reference(new PdfReference(objectNumber, generation)));
                    }
                    break;
                }
                case PdfTokenType.InlineStreamStart:
                {
                    IPdfValue streamValue = ReadInlineStream();
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
