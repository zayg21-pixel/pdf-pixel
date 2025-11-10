using System;

namespace PdfReader.Fonts.PsFont
{
    public enum ValueOperation
    {
        Undefined,
        Div
    }

    public struct Type1CharStringNumber
    {
        private const int DecimalPrecisionFactor = 1000;

        public Type1CharStringNumber(int value)
        {
            Value1 = value;
            Value2 = 0;
            HasSecondValue = false;
            Operation = default;
        }

        public int Value1 { get;  }

        public int Value2 { get; private set; }

        public bool HasSecondValue { get; private set; }

        public ValueOperation Operation { get; private set; }

        public double GetAsDouble()
        {
            if (Operation == ValueOperation.Div && HasSecondValue && Value2 != 0)
            {
                return (double)Value1 / Value2;
            }

            return Value1;
        }

        public static Type1CharStringNumber FromDouble(double value)
        {
            int roundedInteger = (int)Math.Round(value);

            if (Math.Abs(roundedInteger - value) < 1d / DecimalPrecisionFactor)
            {
                return new Type1CharStringNumber(roundedInteger);
            }

            int scaledNumerator = (int)Math.Round(value * DecimalPrecisionFactor);

            var result = new Type1CharStringNumber(scaledNumerator);
            result.SetSecondValue(DecimalPrecisionFactor, ValueOperation.Div);

            return result;
        }

        public void SetSecondValue(int value2, ValueOperation operation)
        {
            Value2 = value2;
            HasSecondValue = true;
            Operation = operation;
        }
    }
}
