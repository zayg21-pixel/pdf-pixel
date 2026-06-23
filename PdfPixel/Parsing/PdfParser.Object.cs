using System.Runtime.CompilerServices;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Reads and parses the next object from the PDF content stream.
    /// </summary>
    /// <returns>A <see cref="PdfObject"/> representing the parsed PDF object, or <see langword="null"/> if the object could
    /// not be read or is invalid.</returns>
    public PdfObject? ReadObject()
    {
        int startPos = Position;

        IPdfValue? first = ReadNextValue();
        IPdfValue? second = ReadNextValue();
        IPdfValue? third = ReadNextValue();

        if (third.AsString() != PdfTokens.Obj)
        {
            Position = startPos;
            return null;
        }

        var objectNumber = (uint)first.AsInteger();
        int generation = second.AsInteger();
        PdfReference reference = new(objectNumber, generation);
        _currentReference = reference;

        IPdfValue? value = ReadNextValue();
        _currentReference = default;

        if (value == null)
        {
            Position = startPos;
            return null;
        }

        PdfObject pdfObject = new(reference, _document, value);

        int preStreamPos = Position;
        IPdfValue? possibleStreamOp = ReadNextValue();

        if (possibleStreamOp.AsString() == PdfTokens.Stream)
        {
            PdfDictionary? dict = value.AsDictionary();
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
    private PdfObjectStreamReference? ReadRawStreamReference(PdfDictionary dict, in PdfReference reference)
    {
        if (dict == null)
        {
            return default;
        }

        SkipSingleEndOfLine();

        int declaredLength = dict.GetValue(PdfTokens.LengthKey).AsInteger();

        int streamStart = Position;

        if (declaredLength <= 0)
        {
            int distanceToEndStream = ScanForToken(PdfTokens.EndStream);
            if (distanceToEndStream <= 0)
            {
                return default;
            }

            declaredLength = distanceToEndStream;
            if (declaredLength > 0 && PeekByte(distanceToEndStream - 1) == LineFeed)
            {
                declaredLength--;
                if (declaredLength > 0 && PeekByte(declaredLength - 1) == CarriageReturn)
                {
                    declaredLength--;
                }
            }
            else if (declaredLength > 0 && PeekByte(distanceToEndStream - 1) == CarriageReturn)
            {
                declaredLength--;
            }

            Position = streamStart + distanceToEndStream;
            Advance(PdfTokens.EndStream.Value.Length);
        }
        else
        {
            int remaining = Length - Position;
            if (declaredLength > remaining)
            {
                declaredLength = remaining;
            }

            Advance(declaredLength);

            SkipSingleEndOfLine();

            Advance(PdfTokens.EndStream.Value.Length);
        }

        return new PdfObjectStreamReference(streamStart, declaredLength, _decrypt);
    }

}
