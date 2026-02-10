using System;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Specifies the operation applied between values in <see cref="Type1CharStringNumber"/>.
/// </summary>
internal enum ValueOperation
{
    /// <summary>
    /// No operation defined.
    /// </summary>
    Undefined,
    /// <summary>
    /// Division operation.
    /// </summary>
    Div
}


/// <summary>
/// Represents a number used in Type1 charstrings, supporting integer and fractional values via division operation.
/// </summary>
internal struct Type1CharStringNumber
{
    private const int DecimalPrecisionFactor = 1000;

    /// <summary>
    /// Initializes a new instance of <see cref="Type1CharStringNumber"/> with an integer value.
    /// </summary>
    /// <param name="value">The integer value.</param>
    public Type1CharStringNumber(int value)
    {
        Value1 = value;
        Value2 = 0;
        HasSecondValue = false;
        Operation = default;
    }

    /// <summary>
    /// Gets the primary value of the number.
    /// </summary>
    public int Value1 { get; }

    /// <summary>
    /// Gets the secondary value, used for division if <see cref="Operation"/> is <see cref="ValueOperation.Div"/>.
    /// </summary>
    public int Value2 { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a secondary value is present.
    /// </summary>
    public bool HasSecondValue { get; private set; }

    /// <summary>
    /// Gets the operation applied between <see cref="Value1"/> and <see cref="Value2"/>.
    /// </summary>
    public ValueOperation Operation { get; private set; }

    /// <summary>
    /// Returns the value as a <see cref="double"/>. If division is specified, returns the result of <c>Value1 / Value2</c>.
    /// </summary>
    public double GetAsDouble()
    {
        if (Operation == ValueOperation.Div && HasSecondValue && Value2 != 0)
        {
            return (double)Value1 / Value2;
        }

        return Value1;
    }

    /// <summary>
    /// Creates a <see cref="Type1CharStringNumber"/> from a double value, using division for fractional values.
    /// </summary>
    /// <param name="value">The double value to represent.</param>
    /// <returns>A <see cref="Type1CharStringNumber"/> representing the value.</returns>
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

    /// <summary>
    /// Sets the secondary value and operation for this number.
    /// </summary>
    /// <param name="value2">The secondary value.</param>
    /// <param name="operation">The operation to apply.</param>
    public void SetSecondValue(int value2, ValueOperation operation)
    {
        Value2 = value2;
        HasSecondValue = true;
        Operation = operation;
    }
}