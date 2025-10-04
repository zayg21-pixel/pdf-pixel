using PdfReader.Rendering.Image.Jpg.Model;
using System;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Maps scan component selectors to SOF component indices with strict validation.
    /// Throws exceptions for any unresolved component selector.
    /// </summary>
    internal sealed class JpgComponentMapper
    {
        /// <summary>
        /// Map SOS scan component selectors to SOF component indices.
        /// </summary>
        /// <param name="header">JPEG header containing SOF component information.</param>
        /// <param name="scan">Scan specification (SOS).</param>
        /// <returns>Array mapping scan component index to SOF component index.</returns>
        public static int[] MapScanToSofIndices(JpgHeader header, JpgScanSpec scan)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (scan == null)
            {
                throw new ArgumentNullException(nameof(scan));
            }
            if (header.Components == null || header.Components.Count == 0)
            {
                throw new InvalidOperationException("SOF contains no components to map.");
            }
            if (scan.Components == null || scan.Components.Count == 0)
            {
                throw new InvalidOperationException("SOS scan specifies no components.");
            }

            int scanCount = scan.Components.Count;
            int[] mappingIndices = new int[scanCount];

            for (int scanComponentIndex = 0; scanComponentIndex < scanCount; scanComponentIndex++)
            {
                byte selectorId = scan.Components[scanComponentIndex].ComponentId;
                int sofIndex = FindComponentIndexById(header, selectorId);
                if (sofIndex < 0)
                {
                    throw new InvalidOperationException($"Unknown SOS component id {selectorId}.");
                }
                mappingIndices[scanComponentIndex] = sofIndex;
            }

            return mappingIndices;
        }

        /// <summary>
        /// Validate a previously produced mapping. Throws if invalid.
        /// </summary>
        public static void ValidateMapping(JpgHeader header, int[] mapping)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }
            if (header.Components == null || header.Components.Count == 0)
            {
                throw new InvalidOperationException("Cannot validate mapping: header has no components.");
            }

            for (int scanIndex = 0; scanIndex < mapping.Length; scanIndex++)
            {
                int sofIndex = mapping[scanIndex];
                if (sofIndex < 0 || sofIndex >= header.Components.Count)
                {
                    throw new InvalidOperationException($"Invalid component mapping: scan component {scanIndex} maps to invalid SOF index {sofIndex}.");
                }
            }
        }

        private static int FindComponentIndexById(JpgHeader header, byte componentId)
        {
            for (int componentIndex = 0; componentIndex < header.Components.Count; componentIndex++)
            {
                if (header.Components[componentIndex].Id == componentId)
                {
                    return componentIndex;
                }
            }
            return -1;
        }
    }
}