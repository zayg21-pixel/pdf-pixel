using System;
using System.Collections.Generic;

namespace PdfPixel.Geometry.PathOperations;

public static partial class PdfStrokeOutlineBuilder
{
    [ThreadStatic]
    private static PdfCurveLengthBuffer? _lengthTableBuffer;

    private static List<List<IPdfContourSegment>> SplitDashes(PdfContourMeasure subPath, float[] dashPattern, float dashPhase)
    {
        List<List<IPdfContourSegment>> pieces = [];
        List<IPdfContourSegment>? current = null;

        if (_lengthTableBuffer == null)
        {
            _lengthTableBuffer = new PdfCurveLengthBuffer();
        }

        PdfCurveLengthBuffer lengthTableBuffer = _lengthTableBuffer;

        int dashIndex = 0;
        float dashRemaining = dashPattern[0];
        var isOn = true;

        float phase = dashPhase;
        while (phase > 0f)
        {
            if (phase < dashRemaining)
            {
                dashRemaining -= phase;
                break;
            }

            phase -= dashRemaining;
            dashIndex = (dashIndex + 1) % dashPattern.Length;
            dashRemaining = dashPattern[dashIndex];
            isOn = !isOn;
        }

        bool startsOn = isOn;

        foreach (IPdfContourSegment segment in subPath.Segments)
        {
            PdfLengthLookup lengthLookup = segment.CreateLengthLookup(lengthTableBuffer);
            float segmentLength = lengthLookup.Length;
            float consumed = 0f;

            while (segmentLength - consumed > PdfVectorMath.Epsilon)
            {
                float remainingInSegment = segmentLength - consumed;

                if (dashRemaining >= remainingInSegment)
                {
                    dashRemaining -= remainingInSegment;
                    IPdfContourSegment piece = segment.Slice(lengthLookup.ParameterAt(consumed), 1f);
                    if (isOn)
                    {
                        (current ??= new List<IPdfContourSegment>()).Add(piece);
                    }

                    consumed = segmentLength;
                }
                else
                {
                    float nextConsumed = consumed + dashRemaining;
                    IPdfContourSegment piece = segment.Slice(lengthLookup.ParameterAt(consumed), lengthLookup.ParameterAt(nextConsumed));
                    if (isOn)
                    {
                        (current ??= new List<IPdfContourSegment>()).Add(piece);
                    }

                    if (isOn && current != null)
                    {
                        pieces.Add(current);
                        current = null;
                    }

                    consumed = nextConsumed;
                    dashIndex = (dashIndex + 1) % dashPattern.Length;
                    dashRemaining = dashPattern[dashIndex];
                    isOn = !isOn;

                    if (pieces.Count > MaxDashPieces)
                    {
                        return new List<List<IPdfContourSegment>> { subPath.Segments };
                    }
                }
            }
        }

        if (current != null)
        {
            if (subPath.IsClosed && startsOn && pieces.Count > 0)
            {
                // The "on" run wraps across the seam at the subpath's start point: this trailing run and
                // the leading run in pieces[0] are one continuous dash, and merge into one join there.
                current.AddRange(pieces[0]);
                pieces[0] = current;
            }
            else
            {
                pieces.Add(current);
            }
        }

        return pieces;
    }
}
