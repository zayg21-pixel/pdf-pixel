namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// The two halves a cubic was split into, in the order they are walked.
/// </summary>
internal readonly struct PdfCubicSplit
{
    public readonly PdfCubicContourSegment Head;

    public readonly PdfCubicContourSegment Tail;

    public PdfCubicSplit(in PdfCubicContourSegment head, in PdfCubicContourSegment tail)
    {
        Head = head;
        Tail = tail;
    }
}
