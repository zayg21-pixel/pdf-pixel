using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents optional content membership — a set of group names
/// combined with a visibility policy.
/// </summary>
public class PdfOptionalContentMembership
{
    /// <summary>
    /// Initializes a new <see cref="PdfOptionalContentMembership"/> with
    /// the specified group names and visibility policy.
    /// </summary>
    public PdfOptionalContentMembership(
        IReadOnlyList<PdfString> groupNames,
        PdfOptionalContentVisibilityPolicy visibilityPolicy)
    {
        GroupNames = groupNames;
        VisibilityPolicy = visibilityPolicy;
    }

    /// <summary>
    /// Names of the optional content groups in this membership.
    /// </summary>
    public IReadOnlyList<PdfString> GroupNames { get; }

    /// <summary>
    /// Policy used to determine visibility from the group states.
    /// </summary>
    public PdfOptionalContentVisibilityPolicy VisibilityPolicy { get; }

    /// <summary>
    /// Parses an optional content entry from a parent dictionary containing an /OC key.
    /// Returns <c>null</c> if the /OC entry is not present.
    /// </summary>
    internal static PdfOptionalContentMembership? FromDictionary(PdfDictionary dictionary)
    {
        PdfDictionary? optionalContentDictionary = dictionary.GetDictionary(PdfTokens.OptionalContentKey);
        if (optionalContentDictionary == null)
        {
            return null;
        }

        return FromOptionalContentDictionary(optionalContentDictionary);
    }

    /// <summary>
    /// Parses an OCG or OCMD dictionary directly into a membership.
    /// </summary>
    internal static PdfOptionalContentMembership? FromOptionalContentDictionary(PdfDictionary optionalContentDictionary)
    {
        PdfOptionalContentType type = optionalContentDictionary
            .GetName(PdfTokens.TypeKey)
            .AsEnum<PdfOptionalContentType>();

        if (type == PdfOptionalContentType.Membership)
        {
            return FromMembershipDictionary(optionalContentDictionary);
        }

        return FromGroupDictionary(optionalContentDictionary);
    }

    private static PdfOptionalContentMembership FromGroupDictionary(PdfDictionary groupDictionary)
    {
        PdfString groupName = groupDictionary.GetName(PdfTokens.NameKey);
        return new PdfOptionalContentMembership(
            new[] { groupName },
            PdfOptionalContentVisibilityPolicy.AllOn);
    }

    private static PdfOptionalContentMembership FromMembershipDictionary(PdfDictionary membershipDictionary)
    {
        PdfOptionalContentVisibilityPolicy policy = membershipDictionary
            .GetName(PdfTokens.VisibilityPolicyKey)
            .AsEnum<PdfOptionalContentVisibilityPolicy>();

        PdfArray? groupsArray = membershipDictionary.GetArray(PdfTokens.OptionalContentGroupsKey);
        if (groupsArray == null)
        {
            PdfDictionary? singleGroup = membershipDictionary.GetDictionary(PdfTokens.OptionalContentGroupsKey);
            if (singleGroup != null)
            {
                PdfString groupName = singleGroup.GetName(PdfTokens.NameKey);
                return new PdfOptionalContentMembership(new[] { groupName }, policy);
            }

            return new PdfOptionalContentMembership([], policy);
        }

        List<PdfString> groupNames = [];
        for (int index = 0; index < groupsArray.Count; index++)
        {
            PdfDictionary? groupDictionary = groupsArray.GetDictionary(index);
            if (groupDictionary != null)
            {
                PdfString groupName = groupDictionary.GetString(PdfTokens.NameKey);
                if (!groupName.IsEmpty)
                {
                    groupNames.Add(groupName);
                }
            }
        }

        return new PdfOptionalContentMembership(groupNames, policy);
    }
}
