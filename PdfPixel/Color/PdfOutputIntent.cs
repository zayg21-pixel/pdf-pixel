using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Color;

/// <summary>
/// Output intent dictionary, an entry of the catalog or page /OutputIntents array, describing the color
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
        DestinationOutputColorSpace = dictionary.Document.ColorSpaceResolver.ResolveIccProfile(dictionary, PdfTokens.DestOutputProfileKey);
        DestinationOutputProfile = DestinationOutputColorSpace?.Profile;

        PdfDictionary? profileReference = dictionary.GetDictionary(PdfTokens.DestOutputProfileRefKey);
        if (profileReference != null)
        {
            DestinationOutputProfileReference = new PdfOutputProfileReference(profileReference);
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
    public IccProfile? DestinationOutputProfile { get; }

    /// <summary>
    /// ICCBased color space of <see cref="DestinationOutputProfile"/>, or <see langword="null"/> when /DestOutputProfile
    /// is absent.
    /// </summary>
    public PdfIccColorSpaceConverter? DestinationOutputColorSpace { get; }

    /// <summary>
    /// Information about referenced ICC profiles (/DestOutputProfileRef, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOutputProfileReference? DestinationOutputProfileReference { get; }

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
            PdfOutputIntent? outputIntent = GetOutputIntent(array, index);
            if (outputIntent != null)
            {
                outputIntents.Add(outputIntent);
            }
        }

        return outputIntents;
    }

    /// <summary>
    /// Returns the output intent at <paramref name="index"/> of <paramref name="array"/>, using the document's cache.
    /// </summary>
    /// <param name="array">Array holding the output intent entry.</param>
    /// <param name="index">Index of the output intent entry.</param>
    /// <returns>The output intent, or <see langword="null"/> when absent or not a dictionary.</returns>
    private static PdfOutputIntent? GetOutputIntent(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    private static PdfOutputIntent? FromObject(PdfObject outputIntentObject)
    {
        PdfDictionary? outputIntentDictionary = outputIntentObject.Value.AsDictionary();
        if (outputIntentDictionary == null)
        {
            return null;
        }

        return new PdfOutputIntent(outputIntentDictionary);
    }
}
