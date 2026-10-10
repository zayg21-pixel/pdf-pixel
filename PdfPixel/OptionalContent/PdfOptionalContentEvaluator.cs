using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.OptionalContent;

/// <summary>
/// Computes the states of optional content groups under a configuration (ISO 32000-2, 8.11).
/// </summary>
internal static class PdfOptionalContentEvaluator
{
    private static readonly PdfString DefaultLanguage = (PdfString)"en";

    /// <summary>
    /// Returns the ON/OFF state of every group that takes part in visibility under <paramref name="configuration"/>.
    /// Groups whose intents do not match the configuration's intent are left out (8.11.2.3).
    /// </summary>
    /// <param name="properties">The document's optional content properties.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="contextEvent">Situation the states are resolved for; null applies View.</param>
    /// <param name="zoom">Magnification for the Zoom usage category; null applies 1.</param>
    /// <param name="language">Language for the Language usage category; null applies the document /Lang, falling back to <c>en</c>.</param>
    /// <param name="user">User name for the User usage category; null skips the category.</param>
    public static Dictionary<PdfReference, bool> Evaluate(
        PdfOptionalContentProperties properties,
        PdfOptionalContentConfiguration configuration,
        PdfOptionalContentEvent? contextEvent,
        float? zoom,
        PdfString? language,
        PdfString? user)
    {
        Dictionary<PdfReference, bool> states = new(properties.Groups.Count);

        foreach (PdfReference group in properties.Groups.Keys)
        {
            bool initialState = configuration.BaseState switch
            {
                PdfOptionalContentBaseState.Off => false,
                PdfOptionalContentBaseState.Unchanged => GetState(properties.DefaultConfiguration, group, true),
                _ => true
            };

            states[group] = GetState(configuration, group, initialState);
        }

        PdfOptionalContentEvent currentEvent = contextEvent ?? PdfOptionalContentEvent.View;
        PdfString currentLanguage = language ?? properties.DocumentLanguage ?? DefaultLanguage;
        float currentZoom = zoom ?? 1f;

        foreach (PdfOptionalContentUsageApplication application in configuration.AutoState)
        {
            if (application.Event == currentEvent)
            {
                ApplyUsageApplication(properties, application, currentZoom, currentLanguage, user, states);
            }
        }

        RemoveUnmatchedIntents(properties, configuration, states);

        return states;
    }

    private static bool GetState(PdfOptionalContentConfiguration configuration, in PdfReference group, bool initialState)
    {
        bool state = initialState;

        if (configuration.OnGroups.Contains(group))
        {
            state = true;
        }

        if (configuration.OffGroups.Contains(group))
        {
            state = false;
        }

        return state;
    }

    /// <summary>
    /// Sets each managed group ON when every consulted category recommends ON, OFF otherwise (Table 101).
    /// </summary>
    private static void ApplyUsageApplication(
        PdfOptionalContentProperties properties,
        PdfOptionalContentUsageApplication application,
        float zoom,
        in PdfString language,
        PdfString? user,
        Dictionary<PdfReference, bool> states)
    {
        HashSet<PdfReference> languageMatches = FindLanguageMatches(properties, application, language);

        foreach (PdfReference group in application.Groups)
        {
            if (!properties.Groups.TryGetValue(group, out PdfOptionalContentGroup? optionalContentGroup) || !states.ContainsKey(group))
            {
                continue;
            }

            PdfOptionalContentUsage? usage = optionalContentGroup.Usage;
            var hasRecommendation = false;
            var isOn = true;

            foreach (PdfOptionalContentUsageCategory category in application.Category)
            {
                bool? recommendation = GetRecommendation(category, usage, zoom, user, group, languageMatches);
                if (recommendation != null)
                {
                    hasRecommendation = true;
                    isOn = isOn && recommendation.Value;
                }
            }

            if (hasRecommendation)
            {
                states[group] = isOn;
            }
        }
    }

    private static bool? GetRecommendation(
        PdfOptionalContentUsageCategory category,
        PdfOptionalContentUsage? usage,
        float zoom,
        PdfString? user,
        in PdfReference group,
        HashSet<PdfReference> languageMatches)
    {
        if (usage == null)
        {
            return null;
        }

        switch (category)
        {
            case PdfOptionalContentUsageCategory.View:
            {
                return usage.IsViewOn;
            }
            case PdfOptionalContentUsageCategory.Print:
            {
                return usage.IsPrintOn;
            }
            case PdfOptionalContentUsageCategory.Export:
            {
                return usage.IsExportOn;
            }
            case PdfOptionalContentUsageCategory.Zoom:
            {
                if (usage.ZoomMin == null && usage.ZoomMax == null)
                {
                    return null;
                }

                return zoom >= (usage.ZoomMin ?? 0f) && zoom < (usage.ZoomMax ?? float.PositiveInfinity);
            }
            case PdfOptionalContentUsageCategory.User:
            {
                if (user == null || usage.UserNames == null)
                {
                    return null;
                }

                foreach (PdfString userName in usage.UserNames)
                {
                    if (userName == user.Value)
                    {
                        return true;
                    }
                }

                return false;
            }
            case PdfOptionalContentUsageCategory.Language:
            {
                if (usage.Lang == null)
                {
                    return null;
                }

                return languageMatches.Contains(group);
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Groups receiving an ON recommendation for the Language category: the exact matches, or when there
    /// are none, the partial matches marked as preferred (Table 101).
    /// </summary>
    private static HashSet<PdfReference> FindLanguageMatches(
        PdfOptionalContentProperties properties,
        PdfOptionalContentUsageApplication application,
        in PdfString language)
    {
        string requested = language.ToString();
        string requestedLanguage = GetPrimaryLanguage(requested);
        HashSet<PdfReference> exactMatches = [];
        HashSet<PdfReference> preferredPartialMatches = [];

        foreach (PdfReference group in application.Groups)
        {
            if (!properties.Groups.TryGetValue(group, out PdfOptionalContentGroup? optionalContentGroup) || optionalContentGroup.Usage?.Lang == null)
            {
                continue;
            }

            string lang = optionalContentGroup.Usage.Lang.Value.ToString();
            if (string.Equals(lang, requested, StringComparison.OrdinalIgnoreCase))
            {
                exactMatches.Add(group);
            }
            else if (optionalContentGroup.Usage.IsLanguagePreferred == true
                && string.Equals(GetPrimaryLanguage(lang), requestedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                preferredPartialMatches.Add(group);
            }
        }

        return (exactMatches.Count > 0) ? exactMatches : preferredPartialMatches;
    }

    private static string GetPrimaryLanguage(string lang)
    {
        int separator = lang.IndexOf('-');
        return (separator < 0) ? lang : lang.Substring(0, separator);
    }

    private static void RemoveUnmatchedIntents(
        PdfOptionalContentProperties properties,
        PdfOptionalContentConfiguration configuration,
        Dictionary<PdfReference, bool> states)
    {
        IReadOnlyList<PdfOptionalContentIntent> configurationIntents = configuration.Intent;

        if (configurationIntents.Count == 0)
        {
            states.Clear();
            return;
        }

        foreach (PdfOptionalContentIntent intent in configurationIntents)
        {
            if (intent.Type == PdfOptionalContentIntentType.All)
            {
                return;
            }
        }

        PdfOptionalContentIntent viewIntent = new(PdfOptionalContentIntentType.View);

        foreach (PdfOptionalContentGroup group in properties.Groups.Values)
        {
            IReadOnlyList<PdfOptionalContentIntent> groupIntents = group.Intent ?? [viewIntent];
            if (!MatchesAny(groupIntents, configurationIntents))
            {
                states.Remove(group.Reference);
            }
        }
    }

    private static bool MatchesAny(IReadOnlyList<PdfOptionalContentIntent> groupIntents, IReadOnlyList<PdfOptionalContentIntent> configurationIntents)
    {
        foreach (PdfOptionalContentIntent groupIntent in groupIntents)
        {
            foreach (PdfOptionalContentIntent configurationIntent in configurationIntents)
            {
                if (groupIntent.Matches(configurationIntent))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
