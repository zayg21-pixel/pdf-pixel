namespace PdfPixel.Models;

/// <summary>
/// File identifier pair (/ID): a permanent identifier set when the file was created and a changing
/// identifier updated with each modification.
/// </summary>
public readonly struct PdfFileIdentifier
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileIdentifier"/> struct.
    /// </summary>
    /// <param name="permanent">Permanent identifier, the first array element.</param>
    /// <param name="changing">Changing identifier, the second array element.</param>
    public PdfFileIdentifier(in PdfString permanent, in PdfString changing)
    {
        Permanent = permanent;
        Changing = changing;
    }

    /// <summary>
    /// Permanent identifier, based on the file contents at the time it was originally created.
    /// </summary>
    public PdfString Permanent { get; }

    /// <summary>
    /// Changing identifier, based on the file contents at the time it was last updated.
    /// </summary>
    public PdfString Changing { get; }

    /// <summary>
    /// Reads an /ID array, or returns <see langword="null"/> when it does not hold two strings.
    /// </summary>
    /// <param name="identifiers">The /ID array.</param>
    internal static PdfFileIdentifier? FromArray(PdfArray? identifiers)
    {
        if (identifiers == null || identifiers.Count < 2)
        {
            return null;
        }

        PdfString? permanent = identifiers.GetString(0);
        PdfString? changing = identifiers.GetString(1);
        if (permanent == null || changing == null)
        {
            return null;
        }

        return new PdfFileIdentifier(permanent.Value, changing.Value);
    }
}
