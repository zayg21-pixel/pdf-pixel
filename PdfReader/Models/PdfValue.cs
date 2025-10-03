using System.Collections.Generic;

namespace PdfReader.Models
{
    public interface IPdfValue
    {
        PdfValueType Type { get; }
    }

    // Union type for PDF values
    public class PdfValue<T> : IPdfValue
    {
        private readonly T _value;
        private readonly PdfValueType _type;

        public PdfValueType Type => _type;
        public T Value => _value;

        internal PdfValue(T value, PdfValueType type)
        {
            _value = value;
            _type = type;
        }

        public override string ToString()
        {
            return _type switch
            {
                PdfValueType.Name => $"/{_value}",
                PdfValueType.String => $"({_value})",
                PdfValueType.HexString => $"<{_value}>",
                PdfValueType.Integer => _value.ToString(),
                PdfValueType.Real => _value.ToString(),
                PdfValueType.Reference => _value.ToString(),
                PdfValueType.Array => _value is List<IPdfValue> list ? $"[{list.Count} items]" : "[array]",
                PdfValueType.Dictionary => _value is PdfDictionary dict ? $"<< {dict.Count} entries >>" : "<<dictionary>>",
                _ => "null"
            };
        }
    }

    // Static factory class for creating PdfValue instances
    public static class PdfValue
    {
        public static PdfValue<string> Name(string value) => new PdfValue<string>(value, PdfValueType.Name);
        public static PdfValue<string> String(string value) => new PdfValue<string>(value, PdfValueType.String);
        public static PdfValue<string> Operator(string value) => new PdfValue<string>(value, PdfValueType.Operator);
        public static PdfValue<string> HexString(string value) => new PdfValue<string>(value, PdfValueType.HexString);
        public static PdfValue<int> Integer(int value) => new PdfValue<int>(value, PdfValueType.Integer);
        public static PdfValue<float> Real(float value) => new PdfValue<float>(value, PdfValueType.Real);
        public static PdfValue<PdfReference> Reference(PdfReference value) => new PdfValue<PdfReference>(value, PdfValueType.Reference);
        public static PdfValue<List<IPdfValue>> Array(List<IPdfValue> value) => new PdfValue<List<IPdfValue>>(value, PdfValueType.Array);
        public static PdfValue<PdfDictionary> Dictionary(PdfDictionary value) => new PdfValue<PdfDictionary>(value, PdfValueType.Dictionary);
    }
}