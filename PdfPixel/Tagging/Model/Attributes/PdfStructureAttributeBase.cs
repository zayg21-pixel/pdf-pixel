namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Base class for attribute objects attached to a structure element through /A or /C.
/// </summary>
public abstract class PdfStructureAttributeBase
{
    /// <summary>
    /// Initializes the attribute with its owner and revision number.
    /// </summary>
    protected PdfStructureAttributeBase(PdfStructureAttributeOwner owner, int? revision)
    {
        Owner = owner;
        Revision = revision;
    }

    /// <summary>
    /// Owner (/O) of the attribute object.
    /// </summary>
    public PdfStructureAttributeOwner Owner { get; }

    /// <summary>
    /// Revision number paired with the attribute object in /A or /C, or <see langword="null"/> when absent.
    /// Deprecated in PDF 2.0.
    /// </summary>
    public int? Revision { get; }
}
