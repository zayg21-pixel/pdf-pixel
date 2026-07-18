using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents optional content membership — a set of optional content group references
/// combined with a visibility policy.
/// </summary>
public class PdfOptionalContentMembership
{
    /// <summary>
    /// Initializes a new <see cref="PdfOptionalContentMembership"/> with
    /// the specified group references and visibility policy.
    /// </summary>
    public PdfOptionalContentMembership(
        IReadOnlyList<PdfReference> groups,
        PdfOptionalContentVisibilityPolicy visibilityPolicy,
        PdfVisibilityExpression? visibilityExpression = null)
    {
        Groups = groups;
        VisibilityPolicy = visibilityPolicy;
        VisibilityExpression = visibilityExpression;
    }

    /// <summary>
    /// References to the optional content groups in this membership.
    /// </summary>
    public IReadOnlyList<PdfReference> Groups { get; }

    /// <summary>
    /// Policy used to determine visibility from the group states.
    /// </summary>
    public PdfOptionalContentVisibilityPolicy VisibilityPolicy { get; }

    /// <summary>
    /// Parsed /VE visibility expression, when present. Per spec, this takes precedence over
    /// <see cref="Groups"/> and <see cref="VisibilityPolicy"/> when evaluating visibility.
    /// </summary>
    public PdfVisibilityExpression? VisibilityExpression { get; }

    /// <summary>
    /// Parses an optional content entry from a parent dictionary containing an /OC key.
    /// Returns <c>null</c> if the /OC entry is not present.
    /// </summary>
    internal static PdfOptionalContentMembership? FromDictionary(PdfDictionary dictionary)
    {
        PdfObject? optionalContentObject = dictionary.GetObject(PdfTokens.OptionalContentKey);
        if (optionalContentObject == null)
        {
            return null;
        }

        return FromOptionalContentObject(optionalContentObject);
    }

    /// <summary>
    /// Parses an OCG or OCMD object directly into a membership.
    /// </summary>
    internal static PdfOptionalContentMembership? FromOptionalContentObject(PdfObject optionalContentObject)
    {
        PdfOptionalContentType type = optionalContentObject.Dictionary
            .GetName(PdfTokens.TypeKey)
            .AsEnum<PdfOptionalContentType>();

        if (type == PdfOptionalContentType.Membership)
        {
            return FromMembershipDictionary(optionalContentObject.Dictionary);
        }

        return FromGroupObject(optionalContentObject);
    }

    private static PdfOptionalContentMembership FromGroupObject(PdfObject groupObject)
        => new([groupObject.Reference], PdfOptionalContentVisibilityPolicy.AllOn);

    private static PdfOptionalContentMembership FromMembershipDictionary(PdfDictionary membershipDictionary)
    {
        PdfOptionalContentVisibilityPolicy policy = membershipDictionary
            .GetName(PdfTokens.VisibilityPolicyKey)
            .AsEnum<PdfOptionalContentVisibilityPolicy>();

        PdfArray? visibilityExpressionArray = membershipDictionary.GetArray(PdfTokens.VisibilityExpressionKey);
        PdfVisibilityExpression? visibilityExpression = (visibilityExpressionArray != null)
            ? PdfVisibilityExpression.Parse(visibilityExpressionArray)
            : null;

        if (visibilityExpression != null)
        {
            return new PdfOptionalContentMembership([], policy, visibilityExpression);
        }

        // /OCGs can be a single OCG reference or an array of OCG references.
        List<PdfObject>? groupObjects = membershipDictionary.GetObjects(PdfTokens.OptionalContentGroupsKey);
        if (groupObjects == null || groupObjects.Count == 0)
        {
            return new PdfOptionalContentMembership([], policy);
        }

        List<PdfReference> groups = new(groupObjects.Count);
        foreach (PdfObject groupObject in groupObjects)
        {
            if (groupObject.Reference.IsValid)
            {
                groups.Add(groupObject.Reference);
            }
        }

        return new PdfOptionalContentMembership(groups, policy);
    }
}
