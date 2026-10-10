using PdfPixel.Models;
using PdfPixel.Tagging.Model.Attributes;
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
        Namespace = tree.GetNamespace(dictionary.GetReference(PdfTokens.NamespaceKey));
        Type = tree.MapRole(RawType, Namespace, out PdfStructureNamespace? typeNamespace);
        TypeNamespace = typeNamespace;
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
    /// Structure type (/S), mapped through the tree's /RoleMap, or through the namespace role maps (/RoleMapNS)
    /// when the element has a <see cref="Namespace"/>.
    /// </summary>
    public PdfString Type { get; }

    /// <summary>
    /// Namespace of <see cref="Type"/>, or <see langword="null"/> for the default standard structure namespace.
    /// </summary>
    public PdfStructureNamespace? TypeNamespace { get; }

    /// <summary>
    /// Structure type (/S) as written, before role mapping.
    /// </summary>
    public PdfString RawType { get; }

    /// <summary>
    /// Namespace (/NS, PDF 2.0) of <see cref="RawType"/>, or <see langword="null"/> for the default standard structure namespace.
    /// </summary>
    public PdfStructureNamespace? Namespace { get; }

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

    /// <summary>
    /// Attribute objects attached through /A in array order, followed by those of the attribute classes
    /// named in /C.
    /// </summary>
    public IEnumerable<PdfStructureAttributeBase> EnumerateAttributes()
    {
        IPdfValue? attributes = _dictionary.GetValue(PdfTokens.AttributesKey);
        PdfArray? attributeArray = attributes.AsArray();

        if (attributeArray == null)
        {
            PdfDictionary? single = attributes.AsDictionary();
            if (single != null)
            {
                yield return PdfStructureAttributeFactory.Create(single, null, Tree);
            }
        }
        else
        {
            for (int index = 0; index < attributeArray.Count; index++)
            {
                PdfDictionary? attribute = attributeArray.GetDictionary(index);
                if (attribute == null)
                {
                    continue;
                }

                int? revision = attributeArray.GetInteger(index + 1);
                if (revision != null)
                {
                    index++;
                }

                yield return PdfStructureAttributeFactory.Create(attribute, revision, Tree);
            }
        }

        IPdfValue? classes = _dictionary.GetValue(PdfTokens.ClassKey);
        PdfArray? classArray = classes.AsArray();

        if (classArray == null)
        {
            PdfString? single = classes.AsName();
            if (single != null)
            {
                foreach (PdfStructureAttributeBase attribute in CreateClassAttributes(single.Value, null))
                {
                    yield return attribute;
                }
            }

            yield break;
        }

        for (int index = 0; index < classArray.Count; index++)
        {
            PdfString? className = classArray.GetName(index);
            if (className == null)
            {
                continue;
            }

            int? revision = classArray.GetInteger(index + 1);
            if (revision != null)
            {
                index++;
            }

            foreach (PdfStructureAttributeBase attribute in CreateClassAttributes(className.Value, revision))
            {
                yield return attribute;
            }
        }
    }

    /// <summary>
    /// Attribute class names (/C) in array order.
    /// </summary>
    public IEnumerable<PdfString> EnumerateClasses()
    {
        IPdfValue? classes = _dictionary.GetValue(PdfTokens.ClassKey);
        PdfArray? classArray = classes.AsArray();

        if (classArray == null)
        {
            PdfString? single = classes.AsName();
            if (single != null)
            {
                yield return single.Value;
            }

            yield break;
        }

        for (int index = 0; index < classArray.Count; index++)
        {
            PdfString? className = classArray.GetName(index);
            if (className != null)
            {
                yield return className.Value;
            }
        }
    }

    // TODO: [LOW] parse /AF associated file specifications

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

    private IEnumerable<PdfStructureAttributeBase> CreateClassAttributes(PdfString className, int? revision)
    {
        PdfDictionary[]? attributes = Tree.GetClassAttributes(className);
        if (attributes == null)
        {
            yield break;
        }

        foreach (PdfDictionary attribute in attributes)
        {
            yield return PdfStructureAttributeFactory.Create(attribute, revision, Tree);
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
