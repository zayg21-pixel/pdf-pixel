using System;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Models
{
    public static class IPdfValueExtension
    {
        // Type-safe getters
        public static string AsName(this IPdfValue value)
        {
            if (value is PdfValue<string> nameValue && nameValue.Type == PdfValueType.Name)
            {
                return nameValue.Value;
            }
            return null;
        }

        public static string AsString(this IPdfValue value)
        {
            if (value is PdfValue<string> nameValue && nameValue.Type == PdfValueType.Name)
            {
                var nameValueString = nameValue.Value;

                if (nameValueString.StartsWith("/"))
                    return nameValueString.Substring(1);

                return nameValueString;
            }
            if (value is PdfValue<string> stringValue && (stringValue.Type == PdfValueType.String || stringValue.Type == PdfValueType.Operator))
            {
                return stringValue.Value;
            }
            else if (value is PdfValue<string> hexValue && hexValue.Type == PdfValueType.HexString)
            {
                var bytes = hexValue.AsHexBytes();

                if (bytes == null) return null;
                return Encoding.ASCII.GetString(bytes);
            }
            return null;
        }

        public static string AsHexString(this IPdfValue value)
        {
            if (value is PdfValue<string> hexStringValue && hexStringValue.Type == PdfValueType.HexString)
            {
                return hexStringValue.Value;
            }
            return null;
        }

        /// <summary>
        /// Convert HexString value into raw bytes. Returns null if not a HexString.
        /// Skips whitespace and pads odd-length nibbles per PDF spec.
        /// </summary>
        public static byte[] AsHexBytes(this IPdfValue value)
        {
            var hex = value.AsHexString();
            if (string.IsNullOrEmpty(hex))
            {
                return null;
            }

            var bytes = new List<byte>(hex.Length / 2);
            int? high = null;
            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                int v;
                if (c >= '0' && c <= '9') v = c - '0';
                else if (c >= 'A' && c <= 'F') v = c - 'A' + 10;
                else if (c >= 'a' && c <= 'f') v = c - 'a' + 10;
                else continue; // ignore non-hex (spaces, newlines)

                if (high == null) high = v;
                else { bytes.Add((byte)((high.Value << 4) | v)); high = null; }
            }
            if (high != null)
            {
                bytes.Add((byte)(high.Value << 4));
            }
            return bytes.ToArray();
        }

        public static int AsInteger(this IPdfValue value)
        {
            if (value is PdfValue<int> intValue && intValue.Type == PdfValueType.Integer)
            {
                return intValue.Value;
            }
            else if (value is PdfValue<float> floatNumber && floatNumber.Type == PdfValueType.Real)
            {
                return (int)floatNumber.Value;
            }

            return 0;
        }

        [Obsolete("Use AsFloat() for float values instead.")]
        public static float AsReal(this IPdfValue value)
        {
            if (value is PdfValue<float> realValue && realValue.Type == PdfValueType.Real)
            {
                return realValue.Value;
            }
            return 0f;
        }

        public static float AsFloat(this IPdfValue value)
        {
            return value.Type switch
            {
                PdfValueType.Integer => value.AsInteger(),
                PdfValueType.Real => value.AsReal(),
                _ => 0f
            };
        }

        public static bool AsBool(this IPdfValue value)
        {
            if (value == null) return false;

            // Names like /true or /false
            if (value.Type == PdfValueType.Name)
            {
                var nameString = value.AsName();
                if (string.IsNullOrEmpty(nameString)) return false;
                if (nameString[0] == '/') nameString = nameString.Substring(1);
                return string.Equals(nameString, "true", StringComparison.OrdinalIgnoreCase);
            }

            // Non-zero numeric considered true per many PDF producers
            if (value.Type == PdfValueType.Integer || value.Type == PdfValueType.Real)
            {
                return value.AsFloat() != 0f;
            }

            // Strings: optional
            var text = value.AsString();
            if (!string.IsNullOrEmpty(text))
            {
                if (text[0] == '/') text = text.Substring(1);
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) return false;
            }

            return false;
        }

        public static PdfReference AsReference(this IPdfValue value)
        {
            if (value is PdfValue<PdfReference> referenceValue && referenceValue.Type == PdfValueType.Reference)
            {
                return referenceValue.Value;
            }
            return new PdfReference(0);
        }

        public static List<IPdfValue> AsArray(this IPdfValue value)
        {
            if (value is PdfValue<List<IPdfValue>> arrayValue && arrayValue.Type == PdfValueType.Array)
            {
                return arrayValue.Value;
            }
            return null;
        }

        public static PdfDictionary AsDictionary(this IPdfValue value)
        {
            if (value is PdfValue<PdfDictionary> dictionaryValue && dictionaryValue.Type == PdfValueType.Dictionary)
            {
                return dictionaryValue.Value;
            }
            return null;
        }

        /// <summary>
        /// Intelligently resolve a value to a dictionary, following references if necessary
        /// </summary>
        public static PdfDictionary ResolveToDictionary(this IPdfValue value, PdfDocument document, int maxDepth = 10)
        {
            var resolved = ResolveToType(value, document, PdfValueType.Dictionary, maxDepth);
            return resolved?.AsDictionary();
        }

        /// <summary>
        /// Intelligently resolve a value to an array, following references if necessary.
        /// Additionally resolves each array item to its first non-reference using ResolveToNonReference.
        /// </summary>
        public static List<IPdfValue> ResolveToArray(this IPdfValue value, PdfDocument document, int maxDepth = 10)
        {
            var resolvedArray = ResolveToType(value, document, PdfValueType.Array, maxDepth);
            var rawArray = resolvedArray?.AsArray();
            if (rawArray == null) return null;

            // Build a resolved copy where each element is dereferenced once to a non-reference value
            var result = new List<IPdfValue>(rawArray.Count);
            for (int i = 0; i < rawArray.Count; i++)
            {
                var item = rawArray[i];
                var nonReferenceItem = item.ResolveToNonReference(document, maxDepth - 1);
                result.Add(nonReferenceItem ?? item);
            }
            return result;
        }

        public static IPdfValue ResolveToNonReference(this IPdfValue value, PdfDocument document, int maxDepth = 10)
        {
            if (value == null || document == null || maxDepth <= 0)
                return null;

            if (value.Type != PdfValueType.Reference)
                return value;

            var reference = value.AsReference();
            if (!reference.IsValid) return null;
            if (!document.Objects.TryGetValue(reference.ObjectNumber, out var referencedObject) || referencedObject == null)
                return null;

            // DirectValue is guaranteed non-null
            return ResolveToNonReference(referencedObject.Value, document, maxDepth - 1);
        }

        public static IPdfValue ResolveToType(this IPdfValue value, PdfDocument document, PdfValueType targetType, int maxDepth = 10)
        {
            if (value == null || document == null || maxDepth <= 0)
                return null;

            if (value.Type == targetType)
                return value;

            if (value.Type == PdfValueType.Reference)
            {
                var reference = value.AsReference();
                if (!reference.IsValid) return null;
                if (!document.Objects.TryGetValue(reference.ObjectNumber, out var referencedObject) || referencedObject == null)
                    return null;

                // Follow DirectValue chain; it's never null
                return ResolveToType(referencedObject.Value, document, targetType, maxDepth - 1);
            }

            if (value.Type == PdfValueType.Dictionary && targetType != PdfValueType.Dictionary)
            {
                var dictionary = value.AsDictionary();
                if (dictionary?.RawValues != null)
                {
                    foreach (var entry in dictionary.RawValues)
                    {
                        var resolved = ResolveToType(entry.Value, document, targetType, maxDepth - 1);
                        if (resolved != null && resolved.Type == targetType)
                            return resolved;
                    }
                }
                return null;
            }

            return null;
        }

        public static List<PdfReference> AsReferenceArray(this IPdfValue value)
        {
            if (value.Type != PdfValueType.Array) return null;

            var array = value.AsArray();
            if (array == null) return null;

            var result = new List<PdfReference>();

            foreach (var item in array)
            {
                if (item.Type == PdfValueType.Reference)
                    result.Add(item.AsReference());
            }

            return result.Count == array.Count ? result : null; // Only return if all items converted
        }
    }
}