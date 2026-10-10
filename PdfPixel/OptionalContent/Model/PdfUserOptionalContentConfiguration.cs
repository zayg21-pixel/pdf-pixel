using PdfPixel.Models;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Optional content configuration resolved for rendering, whose group states the user switches.
/// </summary>
public sealed class PdfUserOptionalContentConfiguration
{
    private readonly PdfOptionalContentProperties _properties;
    private readonly List<PdfOptionalContentGroupState> _groups = [];

    internal PdfUserOptionalContentConfiguration(
        PdfOptionalContentConfiguration source,
        PdfOptionalContentProperties properties,
        PdfOptionalContentEvent? contextEvent,
        float? zoom,
        PdfString? language,
        PdfString? user)
    {
        Source = source;
        _properties = properties;

        Dictionary<PdfReference, bool> resolvedStates = PdfOptionalContentEvaluator.Evaluate(properties, source, contextEvent, zoom, language, user);
        Dictionary<PdfReference, PdfOptionalContentGroupState> statesByReference = new(resolvedStates.Count);

        foreach (PdfOptionalContentGroup group in properties.Groups.Values)
        {
            if (resolvedStates.TryGetValue(group.Reference, out bool isDefaultOn))
            {
                PdfOptionalContentGroupState state = new(group, source.Locked.Contains(group.Reference));
                state.ResetState(isDefaultOn);
                _groups.Add(state);
                statesByReference[group.Reference] = state;
            }
        }

        LinkRadioButtonSiblings(source, statesByReference);
        Items = BuildItems(source.Order, statesByReference);
    }

    /// <summary>
    /// Configuration the states were resolved from; holds the data a user interface is built from.
    /// </summary>
    public PdfOptionalContentConfiguration Source { get; }

    /// <summary>
    /// States of the groups that take part in visibility, in /OCGs order.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentGroupState> Groups => _groups;

    /// <summary>
    /// Presentation tree built from <see cref="PdfOptionalContentConfiguration.Order"/>; groups not listed there
    /// are not presented, and the tree has no effect on visibility.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentItem> Items { get; }

    /// <summary>
    /// Returns a snapshot of the current state of every group, keyed by group reference.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, bool> ToStates()
    {
        Dictionary<PdfReference, bool> states = new(_groups.Count);

        foreach (PdfOptionalContentGroupState state in _groups)
        {
            states[state.Group.Reference] = state.IsOn;
        }

        return states;
    }

    /// <summary>
    /// Resolves every group state again from <see cref="Source"/>, discarding the states the user set.
    /// </summary>
    /// <param name="contextEvent">Situation the states are resolved for; <see langword="null"/> applies <see cref="PdfOptionalContentEvent.View"/>.</param>
    /// <param name="zoom">Magnification for the Zoom usage category; <see langword="null"/> applies 1.</param>
    /// <param name="language">Language for the Language usage category; <see langword="null"/> applies the document /Lang, falling back to <c>en</c>.</param>
    /// <param name="user">User name for the User usage category; <see langword="null"/> skips the category.</param>
    public void Reset(PdfOptionalContentEvent? contextEvent = null, float? zoom = null, PdfString? language = null, PdfString? user = null)
    {
        Dictionary<PdfReference, bool> resolvedStates = PdfOptionalContentEvaluator.Evaluate(_properties, Source, contextEvent, zoom, language, user);

        foreach (PdfOptionalContentGroupState state in _groups)
        {
            state.ResetState(resolvedStates[state.Group.Reference]);
        }
    }

    private static void LinkRadioButtonSiblings(PdfOptionalContentConfiguration source, Dictionary<PdfReference, PdfOptionalContentGroupState> statesByReference)
    {
        foreach (IReadOnlyList<PdfReference> radioButtonGroup in source.RadioButtonGroups)
        {
            foreach (PdfReference member in radioButtonGroup)
            {
                if (!statesByReference.TryGetValue(member, out PdfOptionalContentGroupState? state))
                {
                    continue;
                }

                foreach (PdfReference sibling in radioButtonGroup)
                {
                    if (statesByReference.TryGetValue(sibling, out PdfOptionalContentGroupState? siblingState))
                    {
                        state.AddRadioButtonSibling(siblingState);
                    }
                }
            }
        }
    }

    private static List<PdfOptionalContentItem> BuildItems(
        IReadOnlyList<PdfOptionalContentOrderItem> order,
        Dictionary<PdfReference, PdfOptionalContentGroupState> statesByReference)
    {
        List<PdfOptionalContentItem> items = new(order.Count);

        foreach (PdfOptionalContentOrderItem orderItem in order)
        {
            if (orderItem.Group != null)
            {
                if (statesByReference.TryGetValue(orderItem.Group.Value, out PdfOptionalContentGroupState? state))
                {
                    items.Add(new PdfOptionalContentItem(null, state, new List<PdfOptionalContentItem>()));
                }

                continue;
            }

            items.Add(new PdfOptionalContentItem(orderItem.Label, null, BuildItems(orderItem.Children, statesByReference)));
        }

        return items;
    }
}
