using System;
using System.Collections.Generic;

namespace PdfPixel.Geometry;

public static partial class PdfStrokeOutlineBuilder
{
    [ThreadStatic]
    private static CurveLengthBuffer? _lengthTableBuffer;

    private static List<List<IPathSegment>> SplitDashes(SubPath subPath, float[] dashPattern, float dashPhase)
    {
        List<List<IPathSegment>> pieces = [];
        List<IPathSegment>? current = null;

        if (_lengthTableBuffer == null)
        {
            _lengthTableBuffer = new CurveLengthBuffer();
        }

        CurveLengthBuffer lengthTableBuffer = _lengthTableBuffer;

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

        foreach (IPathSegment segment in subPath.Segments)
        {
            LengthLookup lengthLookup = segment.CreateLengthLookup(lengthTableBuffer);
            float segmentLength = lengthLookup.Length;
            float consumed = 0f;

            while (segmentLength - consumed > Epsilon)
            {
                float remainingInSegment = segmentLength - consumed;

                if (dashRemaining >= remainingInSegment)
                {
                    dashRemaining -= remainingInSegment;
                    IPathSegment piece = segment.Slice(lengthLookup.ParameterAt(consumed), 1f);
                    if (isOn)
                    {
                        (current ??= new List<IPathSegment>()).Add(piece);
                    }

                    consumed = segmentLength;
                }
                else
                {
                    float nextConsumed = consumed + dashRemaining;
                    IPathSegment piece = segment.Slice(lengthLookup.ParameterAt(consumed), lengthLookup.ParameterAt(nextConsumed));
                    if (isOn)
                    {
                        (current ??= new List<IPathSegment>()).Add(piece);
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
                        return new List<List<IPathSegment>> { subPath.Segments };
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
