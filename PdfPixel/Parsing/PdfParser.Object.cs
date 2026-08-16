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

        return FinishReadObject(first, second, startPos);
    }

    /// <summary>
    /// Probes the current position for an <c>N G obj</c> declaration, failing fast when the position
    /// cannot possibly be one. Used by speculative recovery scanning, which probes every byte offset in
    /// a file and would otherwise pay for a full value parse (including unbounded array/dictionary
    /// lookahead) at each failing offset.
    /// </summary>
    public PdfObject? ScanObject()
    {
        int startPos = Position;

        if (!HasLeadingDigit())
        {
            return null;
        }

        IPdfValue? first = ReadNextValue();

        if (!HasLeadingDigit())
        {
            return null;
        }

        IPdfValue? second = ReadNextValue();
        IPdfValue? third = ReadNextValue();

        if (third.AsString() != PdfTokens.Obj)
        {
            Position = startPos;
            return null;
        }

        return FinishReadObject(first, second, startPos);
    }

    private PdfObject? FinishReadObject(IPdfValue? first, IPdfValue? second, int startPos)
    {
        int? objectNumber = first.AsInteger();
        int? generation = second.AsInteger();

        if (objectNumber == null || generation == null)
        {
            Position = startPos;
            return null;
        }

        PdfReference reference = new((uint)objectNumber.Value, generation.Value);
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

        // A missing or non-numeric /Length leaves the length unknown; the endstream scan below recovers it.
        int declaredLength = dict.GetValue(PdfTokens.LengthKey).AsInteger() ?? 0;

        int streamStart = Position;

        if (declaredLength > 0 && declaredLength <= Length - Position)
        {
            Advance(declaredLength);
            SkipSingleEndOfLine();

            if (TryConsumeToken(PdfTokens.EndStream))
            {
                return new PdfObjectStreamReference(streamStart, declaredLength, _decrypt);
            }

            Position = streamStart;
        }

        int distanceToEndStream = ScanForToken(PdfTokens.EndStream);
        if (distanceToEndStream <= 0)
        {
            return default;
        }

        declaredLength = distanceToEndStream - RewindSingleEndOfLine(distanceToEndStream);

        Position = streamStart + distanceToEndStream;
        Advance(PdfTokens.EndStream.Value.Length);

        return new PdfObjectStreamReference(streamStart, declaredLength, _decrypt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasLeadingDigit()
    {
        SkipWhitespacesAndComments();

        return !IsAtEnd
            && PeekByte() >= Zero
            && PeekByte() <= Nine;
    }
}
