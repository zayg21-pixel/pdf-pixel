using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Optional content configuration as defined by the document (/D or an entry of /Configs).
/// </summary>
public sealed class PdfOptionalContentConfiguration
{
    private readonly PdfOptionalContentProperties _properties;

    internal PdfOptionalContentConfiguration(PdfDictionary dictionary, PdfOptionalContentProperties properties, PdfOptionalContentConfiguration? defaultConfiguration)
    {
        _properties = properties;
        Name = dictionary.GetString(PdfTokens.NameKey);
        Creator = dictionary.GetString(PdfTokens.CreatorKey);

        PdfOptionalContentBaseState? baseState = dictionary.GetName(PdfTokens.BaseStateKey)?.AsEnum<PdfOptionalContentBaseState>();
        BaseState = (baseState == null || baseState == PdfOptionalContentBaseState.Unknown) ? PdfOptionalContentBaseState.On : baseState.Value;

        OnGroups = ReadReferenceSet(dictionary.GetArray(PdfTokens.OnKey));
        OffGroups = ReadReferenceSet(dictionary.GetArray(PdfTokens.OffKey));
        List<PdfOptionalContentIntent>? intent = PdfOptionalContentIntent.FromDictionary(dictionary);
        if (intent == null)
        {
            intent = new List<PdfOptionalContentIntent> { new(PdfOptionalContentIntentType.View) };
        }

        Intent = intent;

        List<PdfOptionalContentUsageApplication>? autoState = PdfOptionalContentUsageApplication.FromArray(dictionary.GetArray(PdfTokens.AutoStateKey));
        if (autoState == null)
        {
            autoState = new List<PdfOptionalContentUsageApplication>();
        }

        AutoState = autoState;

        List<PdfOptionalContentOrderItem>? order = PdfOptionalContentOrderItem.FromArray(dictionary.GetArray(PdfTokens.OrderKey));
        if (order != null)
        {
            Order = order;
        }
        else if (defaultConfiguration != null)
        {
            Order = defaultConfiguration.Order;
        }
        else
        {
            Order = new List<PdfOptionalContentOrderItem>();
        }

        PdfOptionalContentListMode? listMode = dictionary.GetName(PdfTokens.ListModeKey)?.AsEnum<PdfOptionalContentListMode>();
        ListMode = (listMode == null || listMode == PdfOptionalContentListMode.Unknown) ? PdfOptionalContentListMode.AllPages : listMode.Value;

        List<IReadOnlyList<PdfReference>>? radioButtonGroups = ReadRadioButtonGroups(dictionary.GetArray(PdfTokens.RBGroupsKey));
        if (radioButtonGroups != null)
        {
            RadioButtonGroups = radioButtonGroups;
        }
        else if (defaultConfiguration != null)
        {
            RadioButtonGroups = defaultConfiguration.RadioButtonGroups;
        }
        else
        {
            RadioButtonGroups = new List<IReadOnlyList<PdfReference>>();
        }

        Locked = ReadReferenceSet(dictionary.GetArray(PdfTokens.LockedKey));
    }

    /// <summary>
    /// Name of the configuration (/Name), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Name { get; }

    /// <summary>
    /// Application or feature that created the configuration (/Creator), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Creator { get; }

    /// <summary>
    /// State of every group not listed in <see cref="OnGroups"/> or <see cref="OffGroups"/> (/BaseState);
    /// <see cref="PdfOptionalContentBaseState.Unchanged"/> keeps the group's state under the document's default configuration.
    /// </summary>
    public PdfOptionalContentBaseState BaseState { get; }

    /// <summary>
    /// Groups that are ON (/ON), unless also listed in <see cref="OffGroups"/> or managed by a matching <see cref="AutoState"/> entry.
    /// </summary>
    public IReadOnlyCollection<PdfReference> OnGroups { get; }

    /// <summary>
    /// Groups that are OFF (/OFF), also when listed in <see cref="OnGroups"/>, unless managed by a matching <see cref="AutoState"/> entry.
    /// </summary>
    public IReadOnlyCollection<PdfReference> OffGroups { get; }

    /// <summary>
    /// Intents whose groups are considered in visibility (/Intent); groups of other intents have no effect,
    /// and an empty list makes all content visible.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentIntent> Intent { get; }

    /// <summary>
    /// Rules that set group states from their usage dictionaries (/AS); an entry whose event matches the
    /// resolution event overrides <see cref="BaseState"/>, <see cref="OnGroups"/> and <see cref="OffGroups"/> for its groups.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentUsageApplication> AutoState { get; }

    /// <summary>
    /// Presentation order of the groups in a user interface (/Order).
    /// </summary>
    public IReadOnlyList<PdfOptionalContentOrderItem> Order { get; }

    /// <summary>
    /// Which groups of <see cref="Order"/> are displayed (/ListMode).
    /// </summary>
    public PdfOptionalContentListMode ListMode { get; }

    /// <summary>
    /// Sets of groups of which at most one is ON at a time in a user interface (/RBGroups).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<PdfReference>> RadioButtonGroups { get; }

    /// <summary>
    /// Groups whose state cannot be changed through a user interface (/Locked).
    /// </summary>
    public IReadOnlyCollection<PdfReference> Locked { get; }

    /// <summary>
    /// Resolves the state of every group under this configuration, for the user to switch.
    /// </summary>
    /// <param name="contextEvent">Situation the states are resolved for; <see langword="null"/> applies <see cref="PdfOptionalContentEvent.View"/>.</param>
    /// <param name="zoom">Magnification for the Zoom usage category; <see langword="null"/> applies 1.</param>
    /// <param name="language">Language for the Language usage category; <see langword="null"/> applies the document /Lang, falling back to <c>en</c>.</param>
    /// <param name="user">User name for the User usage category; <see langword="null"/> skips the category.</param>
    public PdfUserOptionalContentConfiguration ToUserConfiguration(
        PdfOptionalContentEvent? contextEvent = null,
        float? zoom = null,
        PdfString? language = null,
        PdfString? user = null)
    {
        return new(this, _properties, contextEvent, zoom, language, user);
    }

    /// <summary>
    /// Reads an array of indirect references, ignoring other entries, or returns null when absent.
    /// </summary>
    /// <param name="references">The array.</param>
    internal static List<PdfReference>? ReadReferences(PdfArray? references)
    {
        if (references == null)
        {
            return null;
        }

        List<PdfReference> result = new(references.Count);
        for (int index = 0; index < references.Count; index++)
        {
            PdfReference? reference = references.GetReference(index);
            if (reference != null)
            {
                result.Add(reference.Value);
            }
        }

        return result;
    }

    private static HashSet<PdfReference> ReadReferenceSet(PdfArray? references)
    {
        List<PdfReference>? list = ReadReferences(references);
        if (list == null)
        {
            return new HashSet<PdfReference>();
        }

        return new HashSet<PdfReference>(list);
    }

    private static List<IReadOnlyList<PdfReference>>? ReadRadioButtonGroups(PdfArray? groups)
    {
        if (groups == null)
        {
            return null;
        }

        List<IReadOnlyList<PdfReference>> result = new(groups.Count);
        for (int index = 0; index < groups.Count; index++)
        {
            List<PdfReference>? group = ReadReferences(groups.GetArray(index));
            if (group != null)
            {
                result.Add(group);
            }
        }

        return result;
    }
}
