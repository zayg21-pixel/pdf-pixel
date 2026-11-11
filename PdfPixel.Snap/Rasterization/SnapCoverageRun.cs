namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// A run of pixels of one row that an outline covers: fully, or partly with a value per pixel.
/// </summary>
internal readonly struct SnapCoverageRun
{
    public SnapCoverageRun(int left, int count, bool isFull)
    {
        Left = left;
        Count = count;
        IsFull = isFull;
    }

    public int Left { get; }

    public int Count { get; }

    public bool IsFull { get; }
}
