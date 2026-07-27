namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Represents the CID-keyed font ROS (Registry-Ordering-Supplement) operator.
/// Its presence on a <see cref="CffTopDict"/> identifies the font as CID-keyed.
/// </summary>
public class CffRos
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffRos"/> class.
    /// </summary>
    /// <param name="registry">The SID of the Registry string (e.g. "Adobe").</param>
    /// <param name="ordering">The SID of the Ordering string (e.g. "Identity").</param>
    /// <param name="supplement">The Supplement number.</param>
    public CffRos(int registry, int ordering, float supplement)
    {
        Registry = registry;
        Ordering = ordering;
        Supplement = supplement;
    }

    /// <summary>
    /// Gets the SID of the Registry string.
    /// </summary>
    public int Registry { get; }

    /// <summary>
    /// Gets the SID of the Ordering string.
    /// </summary>
    public int Ordering { get; }

    /// <summary>
    /// Gets the Supplement number.
    /// </summary>
    public float Supplement { get; }
}
