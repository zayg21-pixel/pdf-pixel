namespace PdfPixel.Snap.Clipping;

/// <summary>
/// A run of pixels of a <see cref="SnapClip"/> row, fully inside or partly with a value per pixel.
/// </summary>
internal readonly struct SnapClipRun
{
    public SnapClipRun(int left, int count, bool isFull, int valueIndex)
    {
        Left = left;
        Count = count;
        IsFull = isFull;
        ValueIndex = valueIndex;
    }

    public int Left { get; }

    public int Count { get; }

    public bool IsFull { get; }

    /// <summary>
    /// Gets where the values of a partial run start in the clip's values; not read for a full run.
    /// </summary>
    public int ValueIndex { get; }
}
