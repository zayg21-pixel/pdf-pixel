using PdfReader.Text;

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

            return null;
        }

        /// <summary>
        /// Convert HexString value into raw bytes. Returns null if not a HexString.
        /// Skips whitespace and pads odd-length nibbles per PDF spec.
        /// </summary>
        public static byte[] AsStringBytes(this IPdfValue value)
        {
            string stringValue = AsString(value);

            if (stringValue == null)
            {
                return null;
            }

            return EncodingExtensions.PdfDefault.GetBytes(stringValue);
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

        private static float AsReal(this IPdfValue value)
        {
            if (value is PdfValue<float> realValue)
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