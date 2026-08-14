using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Parses the document catalog /OutputIntents and returns the first usable ICC profile.
/// Selection logic prefers well-known /S intent names (e.g. GTS_PDFX, GTS_PDFA1) when multiple are present and
/// falls back to the first valid profile when no preferred intents are found.
/// </summary>
internal sealed class PdfOutputIntentParser
{
    private static readonly PdfString[] PreferredIntentOrder =
    {
        (PdfString)"GTS_PDFX"u8, // PDF/X family
        (PdfString)"GTS_PDFA1"u8, // PDF/A-1
        (PdfString)"GTS_PDFA2"u8, // PDF/A-2
        (PdfString)"GTS_PDFA3"u8, // PDF/A-3
        (PdfString)"GTS_PDFE"u8, // Engineering (rare)
        (PdfString)"ISO_PDF"u8 // Generic ISO intent (fallback)
    };

    private readonly PdfObject _rootObject;
    private readonly ILogger<PdfOutputIntentParser> _logger;

    /// <summary>
    /// Initializes a new <see cref="PdfOutputIntentParser"/> for the given catalog root object.
    /// </summary>
    public PdfOutputIntentParser(PdfObject rootObject, ILogger<PdfOutputIntentParser> logger)
    {
        _rootObject = rootObject ?? throw new ArgumentNullException(nameof(rootObject));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses the first valid output intent profile from the catalog's /OutputIntents array.
    /// Returns <c>null</c> if no valid profile is found.
    /// </summary>
    public IccProfile? ParseFirstOutputIntentProfile()
    {
        PdfDictionary catalogDict = _rootObject.Dictionary;
        if (catalogDict == null)
        {
            return null;
        }

        // Collect all intents.
        List<PdfObject>? intents = catalogDict.GetObjects(PdfTokens.OutputIntentsKey);
        if (intents == null || intents.Count == 0)
        {
            return null;
        }

        // Quick map of preferred order for O(1) rank lookup.
        Dictionary<PdfString, int> preferredRank = new(PreferredIntentOrder.Length);
        for (int index = 0; index < PreferredIntentOrder.Length; index++)
        {
            preferredRank[PreferredIntentOrder[index]] = index;
        }

        IccProfile? firstFallback = null;
        int bestRank = int.MaxValue;
        IccProfile? bestProfile = null;

        foreach (PdfObject intentObj in intents)
        {
            PdfDictionary dict = intentObj.Dictionary;

            // /DestOutputProfile may be indirect or direct stream.
            PdfObject? profileObj = dict.GetObject(PdfTokens.DestOutputProfileKey);
            if (profileObj?.HasStream != true)
            {
                continue; // No stream to parse.
            }

            ReadOnlyMemory<byte> decoded = profileObj.DecodeAsMemory();
            if (decoded.IsEmpty)
            {
                continue;
            }

            byte[] profileBytes = decoded.ToArray();
            IccProfile parsed;
            try
            {
                parsed = IccProfile.Parse(profileBytes);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ICC profile in OutputIntent; skipping entry.");
                continue;
            }
#pragma warning restore CA1031

            if (parsed == null)
            {
                continue;
            }

            // Determine ranking based on /S (intent subtype) if present.
            // NOTE: PdfTokens does not currently expose a dedicated OutputIntent /S key; /S is generic.
            PdfString? intentName = dict.GetName(PdfTokens.SoftMaskSubtypeKey);
            int rank = int.MaxValue;
            if (intentName != null && preferredRank.TryGetValue(intentName.Value, out int r))
            {
                rank = r;
            }

            if (rank < bestRank)
            {
                bestRank = rank;
                bestProfile = parsed;
                if (bestRank == 0)
                {
                    break; // Highest priority reached (PDF/X) — stop early.
                }
            }

            if (firstFallback == null)
            {
                firstFallback = parsed; // Record first valid profile for fallback.
            }
        }

        return bestProfile ?? firstFallback;
    }
}
