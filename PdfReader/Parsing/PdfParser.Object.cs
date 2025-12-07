using System.Runtime.CompilerServices;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Parsing;

partial struct PdfParser
{
    /// <summary>
    /// Reads and parses the next object from the PDF content stream.
    /// </summary>
    /// <returns>A <see cref="PdfObject"/> representing the parsed PDF object, or <see langword="null"/> if the object could
    /// not be read or is invalid.</returns>
    public PdfObject ReadObject()
    {
        int startPos = Position;

        IPdfValue first = ReadNextValue();
        IPdfValue second = ReadNextValue();
        IPdfValue third = ReadNextValue();

        if (third.AsString() != PdfTokens.Obj)
        {
            Position = startPos;
            return null;
        }

        uint objectNumber = (uint)first.AsInteger();
        int generation = second.AsInteger();
        var reference = new PdfReference(objectNumber, generation);
        _currentReference = reference;

        IPdfValue value = ReadNextValue();
        _currentReference = default;

        if (value == null)
        {
            Position = startPos;
            return null;
        }

        var pdfObject = new PdfObject(reference, _document, value);

        int preStreamPos = Position;
        IPdfValue possibleStreamOp = ReadNextValue();

        if (possibleStreamOp.AsString() == PdfTokens.Stream)
        {
            var dict = value.AsDictionary();
            if (dict == null)
            {
                Position = preStreamPos;
            }
            else
            {
                pdfObject.StreamInfo = ReadRawStreamReference(dict, reference);
            }
        }
        else
        {
            Position = preStreamPos;
        }

        return pdfObject;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfObjectStreamReference? ReadRawStreamReference(PdfDictionary dict, PdfReference reference)
    {
        if (dict == null)
        {
            return default;
        }

        SkipSingleEndOfLine();

        int declaredLength = dict.GetValue(PdfTokens.LengthKey).AsInteger();

        if (declaredLength <= 0)
        {
            return default;
        }

        int remaining = Length - Position;
        int streamStart = Position;
        if (declaredLength > remaining)
        {
            declaredLength = remaining;
        }

        Advance(declaredLength);

        SkipSingleEndOfLine();

        Advance(PdfTokens.EndStream.Value.Length);

        return new PdfObjectStreamReference(streamStart, declaredLength, _decrypt);
    }
}
