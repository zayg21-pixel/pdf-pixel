using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// A structure element (/StructElem) in the document structure tree.
/// </summary>
public sealed class PdfStructureElement
{
    private readonly PdfDictionary _dictionary;

    internal PdfStructureElement(PdfDictionary dictionary, PdfStructureTree tree)
    {
        _dictionary = dictionary;
        Tree = tree;
    }

    /// <summary>
    /// The structure tree this element belongs to.
    /// </summary>
    public PdfStructureTree Tree { get; }

    /// <summary>
    /// Structure type (/S), mapped through the tree's /RoleMap.
    /// </summary>
    public PdfString Type => Tree.MapRole(RawType);

    /// <summary>
    /// Structure type (/S) as written, before /RoleMap.
    /// </summary>
    public PdfString RawType => _dictionary.GetNameOrDefault(PdfTokens.StructureTypeKey);

    /// <summary>
    /// Parent structure element (/P), or <see langword="null"/> when the parent is the
    /// structure tree root.
    /// </summary>
    public PdfStructureElement? Parent
    {
        get
        {
            PdfDictionary? parent = _dictionary.GetDictionary(PdfTokens.StructureParentKey);
            if (parent == null || parent.GetName(PdfTokens.TypeKey) == PdfTokens.StructTreeRootKey)
            {
                return null;
            }

            return new PdfStructureElement(parent, Tree);
        }
    }

    /// <summary>
    /// Page (/Pg) the element's content appears on, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? Page => _dictionary.GetReference(PdfTokens.PgKey);

    /// <summary>
    /// Element identifier (/ID), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Id => _dictionary.GetString(PdfTokens.IdKey);

    /// <summary>
    /// Language (/Lang), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Lang => _dictionary.GetString(PdfTokens.LangKey);

    /// <summary>
    /// Replacement text (/ActualText), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ActualText => _dictionary.GetString(PdfTokens.ActualTextKey);

    /// <summary>
    /// Alternative description (/Alt), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Alt => _dictionary.GetString(PdfTokens.AltKey);

    /// <summary>
    /// Expanded form of an abbreviation (/E), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ExpandedForm => _dictionary.GetString(PdfTokens.ExpandedFormKey);

    /// <summary>
    /// Title (/T), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Title => _dictionary.GetString(PdfTokens.TitleKey);

    /// <summary>
    /// Revision number (/R), or <see langword="null"/> when absent.
    /// </summary>
    public int? Revision => _dictionary.GetInteger(PdfTokens.RKey);

    /// <summary>
    /// Pronunciation (/Phoneme), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Phoneme => _dictionary.GetString(PdfTokens.PhonemeKey);

    /// <summary>
    /// Phonetic alphabet (/PhoneticAlphabet) of <see cref="Phoneme"/>,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? PhoneticAlphabet => _dictionary.GetName(PdfTokens.PhoneticAlphabetKey);

    // TODO: [LOW] parse /A and /C attributes, including the revision numbers paired with them

    /// <summary>
    /// Structure elements this element references (/Ref).
    /// </summary>
    public IEnumerable<PdfStructureElement> EnumerateReferenced()
    {
        PdfArray? referenced = _dictionary.GetArray(PdfTokens.ReferencedKey);
        if (referenced == null)
        {
            yield break;
        }

        for (int index = 0; index < referenced.Count; index++)
        {
            PdfDictionary? element = referenced.GetDictionary(index);
            if (element != null)
            {
                yield return new PdfStructureElement(element, Tree);
            }
        }
    }

    // TODO: [LOW] parse /AF associated file specifications, /NS namespace

    /// <summary>
    /// Children (/K) of this element in document order.
    /// </summary>
    public IEnumerable<PdfStructureNode> EnumerateChildren()
    {
        IPdfValue? children = _dictionary.GetValue(PdfTokens.KKey);
        PdfArray? childArray = children.AsArray();

        if (childArray == null)
        {
            PdfStructureNode? single = CreateNode(children);
            if (single != null)
            {
                yield return single.Value;
            }

            yield break;
        }

        for (int index = 0; index < childArray.Count; index++)
        {
            PdfStructureNode? node = CreateNode(childArray.GetValue(index));
            if (node != null)
            {
                yield return node.Value;
            }
        }
    }

    private PdfStructureNode? CreateNode(IPdfValue? child)
    {
        int? mcid = child.AsInteger();
        if (mcid != null)
        {
            return new PdfStructureNode(PdfStructureNodeType.Mcid, mcid, null, null, null, null);
        }

        PdfDictionary? childDictionary = child.AsDictionary();
        if (childDictionary == null)
        {
            return null;
        }

        PdfString? type = childDictionary.GetName(PdfTokens.TypeKey);

        if (type == PdfTokens.ObjectReferenceKey)
        {
            return new PdfStructureNode(
                PdfStructureNodeType.ObjectReference,
                null,
                childDictionary.GetReference(PdfTokens.PgKey),
                null,
                null,
                childDictionary.GetReference(PdfTokens.ObjectKey));
        }

        int? referencedMcid = childDictionary.GetInteger(PdfTokens.MCIDKey);
        if (type == PdfTokens.MarkedContentReferenceKey || referencedMcid != null)
        {
            return new PdfStructureNode(
                PdfStructureNodeType.Mcid,
                referencedMcid,
                childDictionary.GetReference(PdfTokens.PgKey),
                childDictionary.GetReference(PdfTokens.StreamKey),
                childDictionary.GetReference(PdfTokens.StreamOwnerKey),
                null);
        }

        return new PdfStructureNode(new PdfStructureElement(childDictionary, Tree));
    }
}
