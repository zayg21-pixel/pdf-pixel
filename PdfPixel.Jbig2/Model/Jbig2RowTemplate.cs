namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Pre-computed template layout for the sliding-window fast decoder.
/// Holds a flat array of row groups sorted by dy ascending.
/// The last group is always the current row (dy=0), fed from decoded bits.
/// All preceding groups are above-row groups, fed from bitmap row data.
/// </summary>
internal sealed class Jbig2RowTemplate
{
    /// <summary>Row groups sorted by dy ascending. Last entry is dy=0 (current row).</summary>
    internal readonly Jbig2RowGroupInfo[] Groups;

    /// <summary>Original template pixels used to build this template (for diagnostics).</summary>
    internal readonly Jbig2ContextPixel[] Originals;

    /// <summary>Whether this template contains any reference bitmap groups.</summary>
    internal readonly bool HasReference;

    /// <summary>
    /// Pre-computed sliding window group descriptors derived from <see cref="Groups"/>.
    /// Used by the sliding window decoder to avoid per-pixel byte extraction overhead.
    /// </summary>
    internal readonly Jbig2SlidingWindowGroup[] SlidingGroups;

    /// <summary>
    /// True when at least one row-0 group has a Dx that exceeds the depth supported by the
    /// 64-bit rolling buffer in the optimized row decoder. Such templates (notably pattern
    /// dictionaries with AT[0] = (-PW, 0) for large PW) must use the per-pixel slow path.
    /// Threshold is conservative: real cap is -63, we use -32 to keep headroom.
    /// </summary>
    internal readonly bool RequiresSlowPath;

    /// <summary>
    /// Initializes a new row template from the given groups and original pixel definitions.
    /// </summary>
    /// <param name="groups">Pre-computed row groups.</param>
    /// <param name="originals">Original context pixel definitions.</param>
    internal Jbig2RowTemplate(Jbig2RowGroupInfo[] groups, Jbig2ContextPixel[] originals)
    {
        Groups = groups;
        Originals = originals;

        SlidingGroups = new Jbig2SlidingWindowGroup[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            SlidingGroups[i] = new Jbig2SlidingWindowGroup(groups[i]);
            if (groups[i].IsReference != 0)
            {
                HasReference = true;
            }
            if (groups[i].Dy == 0 && -groups[i].MinDx > 32)
            {
                RequiresSlowPath = true;
            }
        }
    }
}
