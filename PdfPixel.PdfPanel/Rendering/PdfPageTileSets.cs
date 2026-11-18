namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// The tile sets of one page: the set shown, and the set of a new scale or tile size being completed, which replaces
/// it once complete.
/// </summary>
internal sealed class PdfPageTileSets
{
    /// <summary>
    /// Gets or sets the set shown.
    /// </summary>
    public PdfPageTiles? Current { get; set; }

    /// <summary>
    /// Gets or sets the set being completed on another grid than <see cref="Current"/>.
    /// </summary>
    public PdfPageTiles? Next { get; set; }

    /// <summary>
    /// Returns the set on the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/>, or
    /// <see langword="null"/> when neither is.
    /// </summary>
    public PdfPageTiles? Find(float scale, int tileSize)
    {
        if (Current != null && Current.IsOnGrid(scale, tileSize))
        {
            return Current;
        }

        if (Next != null && Next.IsOnGrid(scale, tileSize))
        {
            return Next;
        }

        return null;
    }
}
