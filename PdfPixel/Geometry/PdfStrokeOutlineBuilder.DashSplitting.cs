using System.Collections.Generic;

namespace PdfPixel.Geometry;

internal static partial class PdfStrokeOutlineBuilder
{
    private static List<List<IPathSegment>> SplitDashes(SubPath subPath, float[] dashPattern, float dashPhase)
    {
        List<List<IPathSegment>> pieces = [];
        List<IPathSegment>? current = null;
        var lengthTableBuffer = new float[LengthSampleCount + 1];

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
                }
            }
        }

        if (current != null)
        {
            if (subPath.IsClosed && startsOn && pieces.Count > 0) // TODO: investigate
            {
                // The "on" run wraps across the seam at the subpath's start point: the trailing run traced
                // here and the leading run already in pieces[0] are one continuous dash. Merge them so the
                // seam vertex gets a real join instead of end caps on two separate pieces.
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
