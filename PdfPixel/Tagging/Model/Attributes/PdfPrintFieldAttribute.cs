using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Attributes of a non-interactive form field (/O /PrintField).
/// </summary>
public sealed class PdfPrintFieldAttribute : PdfStructureAttributeBase
{
    internal PdfPrintFieldAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.PrintField, revision)
    {
        Role = dictionary.GetName(PdfTokens.RoleKey)?.AsEnum<PdfStructurePrintFieldRole>();
        Checked = (dictionary.GetName(PdfTokens.CheckedKey) ?? dictionary.GetName(PdfTokens.CheckedLowercaseKey))?.AsEnum<PdfStructurePrintFieldState>();
        Description = dictionary.GetString(PdfTokens.DescriptionKey);
    }

    /// <summary>
    /// Type of the form field (Role), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructurePrintFieldRole? Role { get; }

    /// <summary>
    /// State of a radio button or check box (Checked, or the deprecated checked), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructurePrintFieldState? Checked { get; }

    /// <summary>
    /// Alternate name of the field (Desc), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Description { get; }
}
