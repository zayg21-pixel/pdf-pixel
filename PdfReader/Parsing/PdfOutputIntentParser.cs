using System;
using System.Collections.Generic;
using PdfReader.Color.Icc.Model;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Parses the document catalog /OutputIntents and assigns the first usable ICC profile to <see cref="PdfDocument.OutputIntentProfile"/>.
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

        private readonly PdfDocument _document;

        /// <summary>
        /// Construct parser and populate document.OutputIntentProfile.
        /// </summary>
        internal PdfOutputIntentParser(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Parses the first valid output intent profile from the PDF document's catalog and assigns it to the
        /// document's output intent profile.
        /// </summary>
        public void ParseFirstOutputIntentProfile()
        {
            var rootObject = _document.RootObject;
            if (rootObject == null)
            {
                return;
            }

            var catalogDict = rootObject.Dictionary;
            if (catalogDict == null)
            {
                return;
            }

            // Collect all intents.
            List<PdfObject> intents = catalogDict.GetObjects(PdfTokens.OutputIntentsKey);
            if (intents == null || intents.Count == 0)
            {
                return;
            }

            // Quick map of preferred order for O(1) rank lookup.
            var preferredRank = new Dictionary<PdfString, int>(PreferredIntentOrder.Length);
            for (int index = 0; index < PreferredIntentOrder.Length; index++)
            {
                preferredRank[PreferredIntentOrder[index]] = index;
            }

            IccProfile firstFallback = null;
            int bestRank = int.MaxValue;
            IccProfile bestProfile = null;

            foreach (var intentObj in intents)
            {
                var dict = intentObj?.Dictionary;
                if (dict == null)
                {
                    continue;
                }

                // /DestOutputProfile may be indirect or direct stream.
                var profileObj = dict.GetObject(PdfTokens.DestOutputProfileKey);
                if (profileObj == null || !profileObj.HasStream)
                {
                    continue; // No stream to parse.
                }

                var decoded = profileObj.DecodeAsMemory();
                if (decoded.IsEmpty)
                {
                    continue; // Too small to be a valid ICC profile (header alone is128 bytes).
                }

                byte[] profileBytes = decoded.ToArray();
                IccProfile parsed;
                try
                {
                    parsed = IccProfile.Parse(profileBytes);
                }
                catch
                {
                    continue; // Invalid ICC profile – ignore entry.
                }

                if (parsed == null)
                {
                    continue;
                }

                // Determine ranking based on /S (intent subtype) if present.
                var intentName = dict.GetName(PdfTokens.SoftMaskSubtypeKey); // /S lookup (generic key constant).
                                                                             // NOTE: PdfTokens does not currently expose a dedicated OutputIntent /S key; /S is generic. This is intentional.
                int rank = int.MaxValue;
                if (!intentName.IsEmpty && preferredRank.TryGetValue(intentName, out var r))
                {
                    rank = r;
                }

                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestProfile = parsed;
                    if (bestRank == 0)
                    {
                        break; // Highest priority reached (PDF/X) – stop early.
                    }
                }

                if (firstFallback == null)
                {
                    firstFallback = parsed; // Record first valid profile for fallback.
                }
            }

            _document.OutputIntentProfile = bestProfile ?? firstFallback;
        }
    }
}
