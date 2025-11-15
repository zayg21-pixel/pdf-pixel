using System.Collections.Generic;

namespace PdfReader.Models
{
    public interface IPdfValue
    {
        PdfValueType Type { get; }
    }

    public class NullValue : IPdfValue
    {
        public static readonly NullValue Instance = new NullValue();

        public PdfValueType Type => PdfValueType.Null;

        public override string ToString()
        {
            return "null";
        }
    }

    public interface IPdfValue<T> : IPdfValue
    {
        T Value { get; }
    }

    // Union type for PDF values
    public class PdfValue<T> : IPdfValue, IPdfValue<T>
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
                PdfValueType.Null => "null",
                PdfValueType.Name => $"/{_value}",
                PdfValueType.Boolean => _value.ToString(),
                PdfValueType.String => $"({_value})",
                PdfValueType.Operator => _value.ToString(),
                PdfValueType.Integer => _value.ToString(),
                PdfValueType.Real => _value.ToString(),
                PdfValueType.Reference => _value.ToString(),
                PdfValueType.Array => _value is List<IPdfValue> list ? $"[{list.Count} items]" : "[array]",
                PdfValueType.Dictionary => _value is PdfDictionary dict ? $"<< {dict.Count} entries >>" : "<<dictionary>>",
                PdfValueType.InlineStream => "[inline stream]",
                _ => "unknown"
            };
        }
    }

    // Static factory class for creating PdfValue instances
    public static class PdfValueFactory
    {
        public static IPdfValue Null() => NullValue.Instance;
        public static IPdfValue<PdfString> Name(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Name);
        public static IPdfValue<PdfString> String(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.String);
        public static IPdfValue<PdfString> Operator(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.Operator);
        public static IPdfValue<PdfString> InlineStream(PdfString value) => new PdfValue<PdfString>(value, PdfValueType.InlineStream);
        public static IPdfValue<int> Integer(int value) => new PdfValue<int>(value, PdfValueType.Integer);
        public static IPdfValue<float> Real(float value) => new PdfValue<float>(value, PdfValueType.Real);
        public static IPdfValue<bool> Boolean(bool value) => new PdfValue<bool>(value, PdfValueType.Boolean);
        public static IPdfValue<PdfReference> Reference(PdfReference value) => new PdfValue<PdfReference>(value, PdfValueType.Reference);
        public static IPdfValue<PdfArray> Array(PdfArray value) => new PdfValue<PdfArray>(value, PdfValueType.Array);
        public static IPdfValue<PdfDictionary> Dictionary(PdfDictionary value) => new PdfValue<PdfDictionary>(value, PdfValueType.Dictionary);
    }
}