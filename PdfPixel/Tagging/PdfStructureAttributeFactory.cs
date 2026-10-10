using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using PdfPixel.Tagging.Model.Attributes;
using PdfPixel.Text;
using System;

namespace PdfPixel.Tagging;

/// <summary>
/// Creates structure attribute instances from attribute object dictionaries.
/// </summary>
internal static class PdfStructureAttributeFactory
{
    /// <summary>
    /// Creates the attribute type matching the owner (/O) of <paramref name="dictionary"/>.
    /// </summary>
    public static PdfStructureAttributeBase Create(PdfDictionary dictionary, int? revision, PdfStructureTree tree)
    {
        PdfString? ownerName = dictionary.GetName(PdfTokens.AttributeOwnerKey);
        if (ownerName == null)
        {
            return new PdfRawStructureAttribute(dictionary, null, revision);
        }

        return ownerName.Value.AsEnum<PdfStructureAttributeOwner>() switch
        {
            PdfStructureAttributeOwner.Layout => new PdfLayoutAttribute(dictionary, revision),
            PdfStructureAttributeOwner.List => new PdfListAttribute(dictionary, revision),
            PdfStructureAttributeOwner.PrintField => new PdfPrintFieldAttribute(dictionary, revision),
            PdfStructureAttributeOwner.Table => new PdfTableAttribute(dictionary, revision),
            PdfStructureAttributeOwner.Artifact => new PdfArtifactAttribute(dictionary, revision),
            PdfStructureAttributeOwner.UserProperties => new PdfUserPropertiesAttribute(dictionary, revision),
            PdfStructureAttributeOwner.FENote => new PdfFENoteAttribute(dictionary, revision),
            PdfStructureAttributeOwner.NamespaceOwner => new PdfNamespaceOwnerAttribute(
                dictionary,
                tree.GetNamespace(dictionary.GetReference(PdfTokens.NamespaceKey)),
                revision),
            _ => CreateExportFormat(dictionary, ownerName.Value, revision)
        };
    }

    private static PdfStructureAttributeBase CreateExportFormat(PdfDictionary dictionary, in PdfString ownerName, int? revision)
    {
        ReadOnlyMemory<byte> name = ownerName.Value;
        int separator = name.Span.LastIndexOf((byte)'-');
        if (separator > 0)
        {
            PdfString family = new(name.Slice(0, separator));
            PdfStructureAttributeOwner owner = family.AsEnum<PdfStructureAttributeOwner>();
            if (IsExportFormat(owner))
            {
                return new PdfExportFormatAttribute(dictionary, owner, new PdfString(name.Slice(separator + 1)), revision);
            }
        }

        return new PdfRawStructureAttribute(dictionary, ownerName, revision);
    }

    private static bool IsExportFormat(PdfStructureAttributeOwner owner)
    {
        return owner switch
        {
            PdfStructureAttributeOwner.Xml => true,
            PdfStructureAttributeOwner.Html => true,
            PdfStructureAttributeOwner.Oeb => true,
            PdfStructureAttributeOwner.Rtf => true,
            PdfStructureAttributeOwner.Css => true,
            PdfStructureAttributeOwner.Rdfa => true,
            PdfStructureAttributeOwner.Aria => true,
            _ => false
        };
    }
}
