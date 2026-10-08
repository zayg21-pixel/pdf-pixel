namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Pre-computed template layout for the sliding-window fast decoder.
/// Holds a flat array of row groups sorted by dy ascending.
/// The last group is always the current row (dy=0), fed from decoded bits.
/// All preceding groups are above-row groups, fed from bitmap row data.
/// </summary>
internal sealed class Jbig2RowTemplate
{
    /// <summary>
    /// Row groups sorted by dy ascending. Last entry is dy=0 (current row).
    /// </summary>
    public readonly Jbig2RowGroupInfo[] Groups;

    /// <summary>
    /// Original template pixels used to build this template (for diagnostics).
    /// </summary>
    public readonly Jbig2ContextPixel[] Originals;

    /// <summary>
    /// How the rows of this template are decoded.
    /// </summary>
    public readonly Jbig2RowDecodeMode Mode;

    /// <summary>
    /// Initializes a new row template from the given groups and original pixel definitions.
    /// </summary>
    /// <param name="groups">Pre-computed row groups.</param>
    /// <param name="originals">Original context pixel definitions.</param>
    /// <param name="mode">How the rows of the template are decoded.</param>
    public Jbig2RowTemplate(Jbig2RowGroupInfo[] groups, Jbig2ContextPixel[] originals, Jbig2RowDecodeMode mode)
    {
        Groups = groups;
        Originals = originals;
        Mode = mode;
    }
}
