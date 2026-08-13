namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single ppem range within the SFNT "gasp" table: the behavior it asks for applies to every ppem
/// size up to and including <see cref="MaxPpem"/>, above the previous range's limit.
/// </summary>
public readonly struct SfntGaspRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGaspRange"/> struct.
    /// </summary>
    /// <param name="maxPpem">The largest ppem size this range covers.</param>
    /// <param name="behavior">The grid-fitting and smoothing behavior this range asks for.</param>
    public SfntGaspRange(ushort maxPpem, SfntGaspBehavior behavior)
    {
        MaxPpem = maxPpem;
        Behavior = behavior;
    }

    /// <summary>
    /// Gets the largest ppem size this range covers. The last range in a table always states 0xFFFF,
    /// so every size above the preceding ranges falls into it.
    /// </summary>
    public ushort MaxPpem { get; }

    /// <summary>
    /// Gets the grid-fitting and smoothing behavior this range asks for.
    /// </summary>
    public SfntGaspBehavior Behavior { get; }
}
