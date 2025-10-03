using PdfReader.Rendering.Image.Jpg.Model;
using System;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Maps scan component selectors to SOF component indices.
    /// Handles component ID resolution with optional permissive fallbacks.
    /// </summary>
    internal sealed class JpgComponentMapper
    {
        /// <summary>
        /// Map SOS component selectors to SOF component indices.
        /// </summary>
        /// <param name="header">JPEG header containing SOF component information</param>
        /// <param name="scan">Scan specification from SOS</param>
        /// <param name="permissive">Enable fallback strategies for non-standard files</param>
        /// <returns>Array mapping scan component index to SOF component index, or null on error</returns>
        public static int[] MapScanToSofIndices(JpgHeader header, JpgScanSpec scan, bool permissive)
        {
            if (header == null || scan == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Cannot map components: null header or scan");
                return null;
            }

            if (header.Components.Count == 0)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Cannot map components: no SOF components");
                return null;
            }

            if (scan.Components.Count == 0)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Cannot map components: no SOS components");
                return null;
            }

            int scanCount = scan.Components.Count;
            int[] mappingIndices = new int[scanCount];

            for (int scanIndex = 0; scanIndex < scanCount; scanIndex++)
            {
                byte componentId = scan.Components[scanIndex].ComponentId;
                int sofIndex = FindComponentIndexById(header, componentId);

                if (sofIndex < 0 && permissive)
                {
                    sofIndex = TryPermissiveFallback(header, componentId, scanIndex, scanCount);
                }

                if (sofIndex < 0)
                {
                    LogComponentMappingError(header, componentId);
                    return null;
                }

                mappingIndices[scanIndex] = sofIndex;
            }

            return mappingIndices;
        }

        /// <summary>
        /// Find the SOF component index by component ID.
        /// </summary>
        private static int FindComponentIndexById(JpgHeader header, byte componentId)
        {
            for (int i = 0; i < header.Components.Count; i++)
            {
                if (header.Components[i].Id == componentId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Try permissive fallback strategies for component mapping.
        /// </summary>
        private static int TryPermissiveFallback(JpgHeader header, byte componentId, int scanIndex, int scanCount)
        {
            // Strategy 1: Try 1-based index (common in some encoders)
            if (componentId >= 1 && componentId <= header.Components.Count)
            {
                int candidateIndex = componentId - 1;
                Console.Error.WriteLine($"[PdfReader][JPEG] SOS component id {componentId} not found; using 1-based index fallback -> component {candidateIndex}");
                return candidateIndex;
            }

            // Strategy 2: Try 0-based index
            if (componentId < header.Components.Count)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] SOS component id {componentId} not found; using 0-based index fallback -> component {componentId}");
                return componentId;
            }

            // Strategy 3: Map by scan order if counts match
            if (scanCount == header.Components.Count && scanIndex < header.Components.Count)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] SOS component id {componentId} not found; mapping by scan order index {scanIndex}");
                return scanIndex;
            }

            return -1;
        }

        /// <summary>
        /// Log component mapping error with detailed information.
        /// </summary>
        private static void LogComponentMappingError(JpgHeader header, byte componentId)
        {
            var availableIds = new System.Text.StringBuilder();
            availableIds.Append("SOF component ids: ");

            for (int c = 0; c < header.Components.Count; c++)
            {
                if (c > 0)
                {
                    availableIds.Append(", ");
                }

                availableIds.Append(header.Components[c].Id);
            }

            Console.Error.WriteLine($"[PdfReader][JPEG] SOS references unknown component (cs={componentId}). {availableIds}");
        }

        /// <summary>
        /// Validate that a scan component mapping is valid.
        /// </summary>
        public static bool ValidateMapping(JpgHeader header, int[] mapping)
        {
            if (header == null || mapping == null)
            {
                return false;
            }

            for (int i = 0; i < mapping.Length; i++)
            {
                int sofIndex = mapping[i];
                if (sofIndex < 0 || sofIndex >= header.Components.Count)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Invalid component mapping: scan component {i} maps to invalid SOF index {sofIndex}");
                    return false;
                }
            }

            return true;
        }
    }
}