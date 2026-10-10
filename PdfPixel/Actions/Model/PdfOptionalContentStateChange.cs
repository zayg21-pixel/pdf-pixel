using PdfPixel.Models;

namespace PdfPixel.Actions.Model;

/// <summary>
/// One group of a set-OCG-state action and the state applied to it.
/// </summary>
public readonly struct PdfOptionalContentStateChange
{
    internal PdfOptionalContentStateChange(PdfOptionalContentStateOperation operation, in PdfReference group)
    {
        Operation = operation;
        Group = group;
    }

    /// <summary>
    /// State applied to the group.
    /// </summary>
    public PdfOptionalContentStateOperation Operation { get; }

    /// <summary>
    /// Reference of the optional content group.
    /// </summary>
    public PdfReference Group { get; }
}
