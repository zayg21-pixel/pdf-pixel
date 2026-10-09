using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// A structure element (/StructElem) in the document structure tree.
/// </summary>
public sealed class PdfStructureElement : IPdfStructureNode, IEquatable<PdfStructureElement>
{
    private readonly PdfDictionary _dictionary;

    internal PdfStructureElement(PdfDictionary dictionary, PdfReference? reference, PdfStructureTree tree)
    {
        _dictionary = dictionary;
        Reference = reference;
        Tree = tree;
        RawType = dictionary.GetNameOrDefault(PdfTokens.StructureTypeKey);
        Type = tree.MapRole(RawType);
        PageReference = dictionary.GetReference(PdfTokens.PgKey);
        Id = dictionary.GetString(PdfTokens.IdKey);
        Lang = dictionary.GetString(PdfTokens.LangKey);
        ActualText = dictionary.GetString(PdfTokens.ActualTextKey);
        Alt = dictionary.GetString(PdfTokens.AltKey);
        ExpandedForm = dictionary.GetString(PdfTokens.ExpandedFormKey);
        Title = dictionary.GetString(PdfTokens.TitleKey);
        Revision = dictionary.GetInteger(PdfTokens.RKey);
        Phoneme = dictionary.GetString(PdfTokens.PhonemeKey);
        PhoneticAlphabet = dictionary.GetName(PdfTokens.PhoneticAlphabetKey);
    }

    /// <summary>
    /// The structure tree this element belongs to.
    /// </summary>
    public PdfStructureTree Tree { get; }

    /// <summary>
    /// Indirect reference of this element, or <see langword="null"/> when the element is a direct object.
    /// </summary>
    public PdfReference? Reference { get; }

    /// <summary>
    /// Structure type (/S), mapped through the tree's /RoleMap.
    /// </summary>
    public PdfString Type { get; }

    /// <summary>
    /// Structure type (/S) as written, before /RoleMap.
    /// </summary>
    public PdfString RawType { get; }

    /// <summary>
    /// Page (/Pg) the element's content appears on, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Element identifier (/ID), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Id { get; }

    /// <summary>
    /// Language (/Lang), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Lang { get; }

    /// <summary>
    /// Replacement text (/ActualText), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ActualText { get; }

    /// <summary>
    /// Alternative description (/Alt), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Alt { get; }

    /// <summary>
    /// Expanded form of an abbreviation (/E), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ExpandedForm { get; }

    /// <summary>
    /// Title (/T), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Title { get; }

    /// <summary>
    /// Revision number (/R), or <see langword="null"/> when absent.
    /// </summary>
    public int? Revision { get; }

    /// <summary>
    /// Pronunciation (/Phoneme), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Phoneme { get; }

    /// <summary>
    /// Phonetic alphabet (/PhoneticAlphabet) of <see cref="Phoneme"/>,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? PhoneticAlphabet { get; }

    // TODO: [LOW] parse /A and /C attributes, including the revision numbers paired with them

    /// <summary>
    /// Parent structure element (/P), or <see langword="null"/> when the parent is the
    /// structure tree root.
    /// </summary>
    public PdfStructureElement? GetParent()
    {
        PdfDictionary? parent = _dictionary.GetDictionary(PdfTokens.StructureParentKey);
        if (parent == null || parent.GetName(PdfTokens.TypeKey) == PdfTokens.StructTreeRootKey)
        {
            return null;
        }

        return new PdfStructureElement(parent, _dictionary.GetReference(PdfTokens.StructureParentKey), Tree);
    }

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
                yield return new PdfStructureElement(element, referenced.GetReference(index), Tree);
            }
        }
    }

    // TODO: [LOW] parse /AF associated file specifications, /NS namespace

    /// <inheritdoc />
    public IEnumerable<IPdfStructureNode> EnumerateChildren()
    {
        IPdfValue? children = _dictionary.GetValue(PdfTokens.KKey);
        PdfArray? childArray = children.AsArray();

        if (childArray == null)
        {
            IPdfStructureNode? single = CreateNode(children, _dictionary.GetReference(PdfTokens.KKey));
            if (single != null)
            {
                yield return single;
            }

            yield break;
        }

        for (int index = 0; index < childArray.Count; index++)
        {
            IPdfStructureNode? node = CreateNode(childArray.GetValue(index), childArray.GetReference(index));
            if (node != null)
            {
                yield return node;
            }
        }
    }

    private IPdfStructureNode? CreateNode(IPdfValue? child, PdfReference? reference)
    {
        int? mcid = child.AsInteger();
        if (mcid != null)
        {
            return new PdfMarkedContentReference(this, mcid.Value, PageReference, null, null);
        }

        PdfDictionary? childDictionary = child.AsDictionary();
        if (childDictionary == null)
        {
            return null;
        }

        PdfString? type = childDictionary.GetName(PdfTokens.TypeKey);

        if (type == PdfTokens.ObjectReferenceKey)
        {
            PdfReference? referencedObject = childDictionary.GetReference(PdfTokens.ObjectKey);
            if (referencedObject == null)
            {
                return null;
            }

            return new PdfObjectReference(
                this,
                referencedObject.Value,
                childDictionary.GetReference(PdfTokens.PgKey) ?? PageReference);
        }

        if (type == PdfTokens.MarkedContentReferenceKey || childDictionary.HasKey(PdfTokens.MCIDKey))
        {
            int? referencedMcid = childDictionary.GetInteger(PdfTokens.MCIDKey);
            if (referencedMcid == null)
            {
                return null;
            }

            return new PdfMarkedContentReference(
                this,
                referencedMcid.Value,
                childDictionary.GetReference(PdfTokens.PgKey) ?? PageReference,
                childDictionary.GetReference(PdfTokens.StreamKey),
                childDictionary.GetReference(PdfTokens.StreamOwnerKey));
        }

        return new PdfStructureElement(childDictionary, reference, Tree);
    }

    /// <summary>
    /// Determines whether two elements are the same indirect object, or the same instance when
    /// either element is a direct object.
    /// </summary>
    public bool Equals(PdfStructureElement? other)
    {
        if (other == null)
        {
            return false;
        }

        if (Reference == null || other.Reference == null)
        {
            return ReferenceEquals(this, other);
        }

        return Reference.Value == other.Reference.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfStructureElement);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (Reference == null)
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        return Reference.Value.GetHashCode();
    }

    /// <summary>
    /// Determines whether two elements are the same structure element.
    /// </summary>
    public static bool operator ==(PdfStructureElement? left, PdfStructureElement? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two elements are different structure elements.
    /// </summary>
    public static bool operator !=(PdfStructureElement? left, PdfStructureElement? right) => !Equals(left, right);
}
