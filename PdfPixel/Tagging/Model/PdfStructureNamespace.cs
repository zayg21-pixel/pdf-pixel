using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// A namespace (/Namespace) of structure types and attributes (PDF 2.0).
/// </summary>
public sealed class PdfStructureNamespace : IEquatable<PdfStructureNamespace>
{
    private PdfStructureNamespace(PdfDictionary dictionary, PdfReference? reference, in PdfString name)
    {
        Reference = reference;
        Name = name;
        RoleMapNamespace = ReadRoleMap(dictionary.GetDictionary(PdfTokens.RoleMapNamespaceKey));
    }

    /// <summary>
    /// Indirect reference of this namespace, or <see langword="null"/> when the namespace is a direct object.
    /// </summary>
    public PdfReference? Reference { get; }

    /// <summary>
    /// Namespace name (/NS), conventionally a URI.
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Role map (/RoleMapNS) of the structure types in this namespace, or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyDictionary<PdfString, PdfStructureRoleMapping>? RoleMapNamespace { get; }

    // TODO: [LOW] parse /Schema file specification

    /// <summary>
    /// A namespace over <paramref name="dictionary"/>, or <see langword="null"/> when it has no namespace name (/NS).
    /// </summary>
    internal static PdfStructureNamespace? FromDictionary(PdfDictionary? dictionary, PdfReference? reference)
    {
        PdfString? name = dictionary?.GetString(PdfTokens.NamespaceKey);
        if (dictionary == null || name == null)
        {
            return null;
        }

        return new PdfStructureNamespace(dictionary, reference, name.Value);
    }

    private static Dictionary<PdfString, PdfStructureRoleMapping>? ReadRoleMap(PdfDictionary? roleMap)
    {
        if (roleMap == null)
        {
            return null;
        }

        Dictionary<PdfString, PdfStructureRoleMapping> mappings = [];

        foreach (PdfString sourceType in roleMap.RawValues.Keys)
        {
            PdfString? targetType = roleMap.GetName(sourceType);
            if (targetType != null)
            {
                mappings[sourceType] = new PdfStructureRoleMapping(targetType.Value, null);
                continue;
            }

            PdfArray? target = roleMap.GetArray(sourceType);
            PdfString? namespacedType = target?.GetName(0);
            if (target != null && namespacedType != null)
            {
                mappings[sourceType] = new PdfStructureRoleMapping(namespacedType.Value, target.GetReference(1));
            }
        }

        return mappings;
    }

    /// <summary>
    /// Determines whether two namespaces are the same indirect object, or the same instance when
    /// either namespace is a direct object.
    /// </summary>
    public bool Equals(PdfStructureNamespace? other)
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
    public override bool Equals(object? obj) => Equals(obj as PdfStructureNamespace);

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
    /// Determines whether two namespaces are the same namespace.
    /// </summary>
    public static bool operator ==(PdfStructureNamespace? left, PdfStructureNamespace? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two namespaces are different namespaces.
    /// </summary>
    public static bool operator !=(PdfStructureNamespace? left, PdfStructureNamespace? right) => !Equals(left, right);
}
