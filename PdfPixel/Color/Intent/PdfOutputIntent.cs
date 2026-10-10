using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color.Intent;

/// <summary>
/// Output intent dictionary, an entry of the catalog /OutputIntents array, describing the color
/// characteristics of an output device.
/// </summary>
public sealed class PdfOutputIntent
{
    internal PdfOutputIntent(PdfDictionary dictionary)
    {
        PdfString? subtype = dictionary.GetName(PdfTokens.OutputIntentSubtypeKey);
        Subtype = subtype?.AsEnum<PdfOutputIntentSubtype>();
        RawSubtype = (Subtype == PdfOutputIntentSubtype.Raw) ? subtype : null;
        OutputCondition = dictionary.GetString(PdfTokens.OutputConditionKey);
        OutputConditionIdentifier = dictionary.GetString(PdfTokens.OutputConditionIdentifierKey);
        RegistryName = dictionary.GetString(PdfTokens.RegistryNameKey);
        Info = dictionary.GetString(PdfTokens.OutputIntentInfoKey);
        DestOutputProfile = ReadProfile(dictionary.GetObject(PdfTokens.DestOutputProfileKey));

        PdfDictionary? profileReference = dictionary.GetDictionary(PdfTokens.DestOutputProfileRefKey);
        if (profileReference != null)
        {
            DestOutputProfileRef = new PdfOutputProfileReference(profileReference);
        }
    }

    /// <summary>
    /// Output intent subtype (/S), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOutputIntentSubtype? Subtype { get; }

    /// <summary>
    /// Subtype name as written when <see cref="Subtype"/> is <see cref="PdfOutputIntentSubtype.Raw"/>,
    /// otherwise <see langword="null"/>.
    /// </summary>
    public PdfString? RawSubtype { get; }

    /// <summary>
    /// Human-readable name of the intended output device or production condition (/OutputCondition),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? OutputCondition { get; }

    /// <summary>
    /// Identifier of the intended output device or production condition (/OutputConditionIdentifier),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? OutputConditionIdentifier { get; }

    /// <summary>
    /// Registry in which <see cref="OutputConditionIdentifier"/> is defined (/RegistryName),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RegistryName { get; }

    /// <summary>
    /// Additional information about the intended output device or production condition (/Info),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Info { get; }

    /// <summary>
    /// ICC profile of the output device (/DestOutputProfile), or <see langword="null"/> when absent or unreadable.
    /// </summary>
    public IccProfile? DestOutputProfile { get; }

    /// <summary>
    /// Information about referenced ICC profiles (/DestOutputProfileRef, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOutputProfileReference? DestOutputProfileRef { get; }

    /// <summary>
    /// The output intents in <paramref name="array"/>, or <see langword="null"/> when it is absent.
    /// </summary>
    internal static List<PdfOutputIntent>? FromArray(PdfArray? array)
    {
        if (array == null)
        {
            return null;
        }

        List<PdfOutputIntent> outputIntents = new(array.Count);
        for (int index = 0; index < array.Count; index++)
        {
            PdfDictionary? entry = array.GetDictionary(index);
            if (entry != null)
            {
                outputIntents.Add(new PdfOutputIntent(entry));
            }
        }

        return outputIntents;
    }

    /// <summary>
    /// Converter for the profile of the output intent used in rendering: the first with a usable profile,
    /// preferring PDF/X, then PDF/A, then PDF/E. <see langword="null"/> when none has a usable profile.
    /// </summary>
    internal static PdfIccColorSpaceConverter? CreateConverter(IReadOnlyList<PdfOutputIntent>? outputIntents)
    {
        if (outputIntents == null)
        {
            return null;
        }

        IccProfile? bestProfile = null;
        int bestRank = int.MaxValue;

        foreach (PdfOutputIntent outputIntent in outputIntents)
        {
            IccProfile? profile = outputIntent.DestOutputProfile;
            if (profile == null || profile.ChannelsCount == 0)
            {
                continue;
            }

            int rank = GetRank(outputIntent.Subtype);
            if (rank < bestRank)
            {
                bestRank = rank;
                bestProfile = profile;
            }
        }

        if (bestProfile == null)
        {
            return null;
        }

        return new PdfIccColorSpaceConverter(bestProfile.ChannelsCount, default, bestProfile);
    }

    private static int GetRank(PdfOutputIntentSubtype? subtype)
    {
        switch (subtype)
        {
            case PdfOutputIntentSubtype.GtsPdfX:
                return 0;

            case PdfOutputIntentSubtype.GtsPdfA1:
                return 1;

            case PdfOutputIntentSubtype.IsoPdfE1:
                return 2;

            default:
                return 3;
        }
    }

    private static IccProfile? ReadProfile(PdfObject? profileObject)
    {
        if (profileObject?.HasStream != true)
        {
            return null;
        }

        ReadOnlyMemory<byte> profileData = profileObject.Stream.DecodeAsMemory();
        if (profileData.IsEmpty)
        {
            return null;
        }

#pragma warning disable CA1031
        try
        {
            return IccProfile.Parse(profileData.ToArray());
        }
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
