using PdfReader.Text;
using System;

namespace PdfReader.Models
{
    public static class IPdfValueExtension
    {
        // Type-safe getters
        public static PdfString AsName(this IPdfValue value)
        {
            if (value is IPdfValue<PdfString> nameValue && nameValue.Type == PdfValueType.Name)
            {
                return nameValue.Value;
            }

            return default;
        }

        public static PdfString AsString(this IPdfValue value)
        {
            if (value is IPdfValue<PdfString> stringValue && (stringValue.Type == PdfValueType.String || stringValue.Type == PdfValueType.Operator))
            {
                return stringValue.Value;
            }

            return default;
        }

        /// <summary>
        /// Convert HexString value into raw bytes. Returns null if not a HexString.
        /// Skips whitespace and pads odd-length nibbles per PDF spec.
        /// </summary>
        public static ReadOnlyMemory<byte> AsStringBytes(this IPdfValue value)
        {
            var stringValue = AsString(value);

            if (stringValue.IsEmpty)
            {
                return null;
            }

            return stringValue.Value;
        }

        public static int AsInteger(this IPdfValue value)
        {
            if (value is IPdfValue<int> intValue && intValue.Type == PdfValueType.Integer)
            {
                return intValue.Value;
            }
            else if (value is IPdfValue<float> floatNumber && floatNumber.Type == PdfValueType.Real)
            {
                return (int)floatNumber.Value;
            }

            return 0;
        }

        private static float AsReal(this IPdfValue value)
        {
            if (value is IPdfValue<float> realValue)
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
            if (value is PdfValue<bool> booleanValue)
            {
                return booleanValue.Value;
            }


            return false;
        }

        public static PdfArray AsArray(this IPdfValue value)
        {
            if (value is PdfValue<PdfArray> arrayValue && arrayValue.Type == PdfValueType.Array)
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

        public static IPdfValue ResolveToNonReference(this IPdfValue value, PdfDocument document, int maxDepth = 10)
        {
            if (value == null || document == null || maxDepth <= 0)
                return null;

            if (value.Type != PdfValueType.Reference)
                return value;

            var reference = (IPdfValue<PdfReference>)value;

            var referencedObject = document.GetObject(reference.Value);

            if (referencedObject == null)
            {
                return null;
            }

            return ResolveToNonReference(referencedObject.Value, document, maxDepth - 1);
        }
    }
}