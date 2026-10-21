using PdfPixel.Files;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Tagging.Attributes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Tagging;

/// <summary>
/// A structure element (/StructElem) in the document structure tree.
/// </summary>
public sealed class PdfStructureElement : IPdfStructureNode, IPdfAssociatedFilesHost, IEquatable<PdfStructureElement>
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
        AlternateDescription = dictionary.GetString(PdfTokens.AltKey);
        ExpandedForm = dictionary.GetString(PdfTokens.ExpandedFormKey);
        Title = dictionary.GetString(PdfTokens.TitleKey);
        Revision = dictionary.GetInteger(PdfTokens.RKey);
        Phoneme = dictionary.GetString(PdfTokens.PhonemeKey);
        PhoneticAlphabet = dictionary.GetName(PdfTokens.PhoneticAlphabetKey);
        AssociatedFiles = PdfFileSpecification.GetFileSpecifications(dictionary.GetArray(PdfTokens.AssociatedFilesKey));
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

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

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
    public PdfString? AlternateDescription { get; }

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
    public PdfStructureElement? GetParent() => GetStructureElement(_dictionary, PdfTokens.StructureParentKey, Tree);

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
            PdfStructureElement? element = GetStructureElement(referenced, index, Tree);
            if (element != null)
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// Attribute objects attached through /A in array order, followed by those of the attribute classes
    /// named in /C.
    /// </summary>
    public IEnumerable<PdfStructureAttribute> EnumerateAttributes()
    {
        PdfValue? attributes = _dictionary.GetValue(PdfTokens.AttributesKey);
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

        PdfValue? classes = _dictionary.GetValue(PdfTokens.ClassKey);
        PdfArray? classArray = classes.AsArray();

        if (classArray == null)
        {
            PdfString? single = classes.AsName();
            if (single != null)
            {
                foreach (PdfStructureAttribute attribute in CreateClassAttributes(single.Value, null))
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

            foreach (PdfStructureAttribute attribute in CreateClassAttributes(className.Value, revision))
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
        PdfValue? classes = _dictionary.GetValue(PdfTokens.ClassKey);
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

    /// <inheritdoc />
    public IEnumerable<IPdfStructureNode> EnumerateChildren()
    {
        PdfValue? children = _dictionary.GetValue(PdfTokens.KKey);
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

    /// <summary>
    /// Returns the structure element held by <paramref name="key"/> of <paramref name="owner"/>, using the document's cache if available.
    /// </summary>
    /// <param name="owner">Dictionary holding the structure element entry.</param>
    /// <param name="key">Key of the structure element entry.</param>
    /// <param name="tree">Structure tree the element belongs to.</param>
    /// <returns>The structure element, or <see langword="null"/> when absent, not a dictionary or the structure tree root.</returns>
    internal static PdfStructureElement? GetStructureElement(PdfDictionary owner, in PdfString key, PdfStructureTree tree)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, elementObject => FromObject(elementObject, tree));

    /// <summary>
    /// Returns the structure element at <paramref name="index"/> of <paramref name="array"/>, using the document's cache if available.
    /// </summary>
    /// <param name="array">Array holding the structure element entry.</param>
    /// <param name="index">Index of the structure element entry.</param>
    /// <param name="tree">Structure tree the element belongs to.</param>
    /// <returns>The structure element, or <see langword="null"/> when absent, not a dictionary or the structure tree root.</returns>
    internal static PdfStructureElement? GetStructureElement(PdfArray array, int index, PdfStructureTree tree)
        => array.Document.ObjectCache.GetOrCreate(array, index, elementObject => FromObject(elementObject, tree));

    /// <summary>
    /// Returns the structure element <paramref name="reference"/> refers to, using the document's cache if available.
    /// </summary>
    /// <param name="document">Document holding the structure element.</param>
    /// <param name="reference">Reference of the structure element.</param>
    /// <param name="tree">Structure tree the element belongs to.</param>
    /// <returns>The structure element, or <see langword="null"/> when absent, not a dictionary or the structure tree root.</returns>
    internal static PdfStructureElement? GetStructureElement(IPdfDocumentInternal document, PdfReference? reference, PdfStructureTree tree)
        => document.ObjectCache.GetOrCreate(reference, elementObject => FromObject(elementObject, tree));

    private static PdfStructureElement? FromObject(PdfObject elementObject, PdfStructureTree tree)
    {
        PdfDictionary? dictionary = elementObject.Value.AsDictionary();
        if (dictionary == null || dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.StructTreeRootKey)
        {
            return null;
        }

        return new PdfStructureElement(dictionary, elementObject.Reference, tree);
    }

    private IEnumerable<PdfStructureAttribute> CreateClassAttributes(PdfString className, int? revision)
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

    private IPdfStructureNode? CreateNode(PdfValue? child, PdfReference? reference)
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

        if (reference == null)
        {
            return new PdfStructureElement(childDictionary, null, Tree);
        }

        return GetStructureElement(_dictionary.Document, reference, Tree);
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
