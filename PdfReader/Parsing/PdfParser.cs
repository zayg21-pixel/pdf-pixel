using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfReader.Parsing
{
    // TODO: handle decryption here.
    internal ref partial struct PdfParser
    {
        // Boolean literal constants
        private readonly ReadOnlySpan<byte> TrueValue = "true"u8;
        private readonly ReadOnlySpan<byte> FalseValue = "false"u8;

        private int _lastSetPostion = 0;
        private readonly BinaryReader _reader;
        private readonly bool _streamMode;
        private PdfParseContext _parseContext;
        private readonly PdfDocument _document; // Needed for constructing arrays/dictionaries
        private readonly bool _allowReferences;

        public PdfParser(PdfParseContext parseContext, PdfDocument document, bool allowReferences)
        {
            _parseContext = parseContext;
            _document = document;
            _allowReferences = allowReferences;
        }

        public PdfParser(Stream stream, PdfDocument document, bool allowReferences)
        {
            _reader = new BinaryReader(stream, Encoding.ASCII);
            _document = document;
            _allowReferences = allowReferences;
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

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetLength();
        }

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
                            int objectNumber = values[values.Count - 2].AsInteger();
                            values.RemoveRange(values.Count - 2, 2);
                            values.Add(PdfValue.Reference(new PdfReference(objectNumber, generation)));
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
}
