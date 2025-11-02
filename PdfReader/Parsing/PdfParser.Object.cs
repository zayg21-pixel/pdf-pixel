using System;
using System.Runtime.CompilerServices;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    internal ref partial struct PdfParser
    {
        /// <summary>
        /// Reads and parses the next object from the PDF content stream.
        /// </summary>
        /// <returns>A <see cref="PdfObject"/> representing the parsed PDF object, or <see langword="null"/> if the object could
        /// not be read or is invalid.</returns>
        public PdfObject ReadObject()
        {
            int startPos = _parseContext.Position;

            IPdfValue first = ReadNextValue();
            IPdfValue second = ReadNextValue();
            IPdfValue third = ReadNextValue();

            if (third.AsString() != PdfTokens.Obj)
            {
                _parseContext.Position = startPos;
                return null;
            }

            int objectNumber = first.AsInteger();
            int generation = second.AsInteger();
            var reference = new PdfReference(objectNumber, generation);

            IPdfValue value = ReadNextValue();
            if (value == null)
            {
                _parseContext.Position = startPos;
                return null;
            }

            var pdfObject = new PdfObject(reference, _document, value);

            int preStreamPos = _parseContext.Position;
            IPdfValue possibleStreamOp = ReadNextValue();

            if (possibleStreamOp.AsString() == PdfTokens.Stream)
            {
                var dict = value.AsDictionary();
                if (dict == null)
                {
                    _parseContext.Position = preStreamPos;
                }
                else
                {
                    var rawStream = ReadRawStream(dict, reference);
                    if (!rawStream.IsEmpty)
                    {
                        pdfObject.StreamData = rawStream;
                    }
                }
            }
            else
            {
                _parseContext.Position = preStreamPos;
            }

            return pdfObject;
        }

        /// <summary>
        /// Read raw (undecoded) stream bytes based on the /Length entry of the provided dictionary.
        /// Handles indirect /Length references and consumes the trailing 'endstream' keyword.
        /// Does not apply filter decoding; caller is responsible for later decoding via PdfStreamDecoder.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyMemory<byte> ReadRawStream(PdfDictionary dict, PdfReference reference)
        {
            if (dict == null)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            SkipSingleEndOfLine();

            int declaredLength = dict.GetValue(PdfTokens.LengthKey).AsInteger();

            if (declaredLength <= 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            int remaining = _parseContext.Length - _parseContext.Position;
            if (declaredLength > remaining)
            {
                declaredLength = remaining;
            }

            ReadOnlyMemory<byte> data;

            if (_parseContext.IsSingleMemory)
            {
                data = _parseContext.OriginalMemory.Slice(_parseContext.Position, declaredLength);
            }
            else
            {
                data = _parseContext.GetSlice(_parseContext.Position, declaredLength).ToArray();
            }

            _parseContext.Advance(declaredLength);

            SkipSingleEndOfLine();
            ConsumeKeyword(PdfTokens.Endstream);

            if (_document?.Decryptor != null)
            {
                data = _document.Decryptor.DecryptBytes(data, reference);
            }

            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSingleEndOfLine()
        {
            if (_parseContext.IsAtEnd)
            {
                return;
            }
            byte b = _parseContext.PeekByte();
            if (b == (byte)'\r')
            {
                _parseContext.Advance(1);
                if (!_parseContext.IsAtEnd && _parseContext.PeekByte() == (byte)'\n')
                {
                    _parseContext.Advance(1);
                }
            }
            else if (b == (byte)'\n')
            {
                _parseContext.Advance(1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConsumeKeyword(ReadOnlySpan<byte> keyword)
        {
            SkipWhitespacesAndComments();
            if (_parseContext.MatchSequenceAt(_parseContext.Position, keyword))
            {
                _parseContext.Advance(keyword.Length);
            }
        }
    }
}
