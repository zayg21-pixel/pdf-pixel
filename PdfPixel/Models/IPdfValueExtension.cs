using System;

namespace PdfPixel.Models;

/// <summary>
/// Extension methods for extracting strongly-typed values from <see cref="IPdfValue"/> instances.
/// Provides conversion and type-checking helpers for PDF value types.
/// </summary>
public static class IPdfValueExtension
{
    /// <summary>
    /// Returns the value as a PDF name if the type is <see cref="PdfValueType.Name"/>; otherwise returns <c>default</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfString"/> representing the name, or <c>default</c> if not a name.</returns>
    public static PdfString AsName(this IPdfValue value)
    {
        if (value is IPdfValue<PdfString> nameValue && nameValue.Type == PdfValueType.Name)
        {
            return nameValue.Value;
        }

        return default;
    }

    /// <summary>
    /// Returns the value as a PDF string if the type is <see cref="PdfValueType.String"/> or <see cref="PdfValueType.HexString"/>; otherwise returns <c>default</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfString"/> value, or <c>default</c> if not a string type.</returns>
    public static PdfString AsString(this IPdfValue value)
    {
        if (value is IPdfValue<PdfString> stringValue)
        {
            return stringValue.Value;
        }

        return default;
    }

    /// <summary>
    /// Returns the raw bytes of a PDF string value. Returns <c>null</c> if not a string or if the string is empty.
    /// Skips whitespace and pads odd-length nibbles per PDF spec for hex strings.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The raw byte content of the string, or <c>null</c> if not a string or empty.</returns>
    public static ReadOnlyMemory<byte> AsStringBytes(this IPdfValue value)
    {
        var stringValue = AsString(value);

        if (stringValue.IsEmpty)
        {
            return null;
        }

        return stringValue.Value;
    }

    /// <summary>
    /// Returns the value as an integer if the type is <see cref="PdfValueType.Integer"/> or <see cref="PdfValueType.Real"/>; otherwise returns 0.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The integer value, or 0 if not numeric.</returns>
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

    /// <summary>
    /// Returns the value as a float if the type is <see cref="PdfValueType.Real"/>; otherwise returns 0.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The float value, or 0 if not a real number.</returns>
    private static float AsReal(this IPdfValue value)
    {
        if (value is IPdfValue<float> realValue)
        {
            return realValue.Value;
        }
        return 0f;
    }

    /// <summary>
    /// Returns the value as a float if the type is <see cref="PdfValueType.Integer"/> or <see cref="PdfValueType.Real"/>; otherwise returns 0.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The float value, or 0 if not numeric.</returns>
    public static float AsFloat(this IPdfValue value)
    {
        return value.Type switch
        {
            PdfValueType.Integer => value.AsInteger(),
            PdfValueType.Real => value.AsReal(),
            _ => 0f,
        };
    }

    /// <summary>
    /// Returns the value as a boolean if the type is <see cref="PdfValueType.Boolean"/>; otherwise returns <c>false</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The boolean value, or <c>false</c> if not a boolean.</returns>
    public static bool AsBoolean(this IPdfValue value)
    {
        if (value is PdfValue<bool> booleanValue)
        {
            return booleanValue.Value;
        }

        return false;
    }

    /// <summary>
    /// Returns the value as a PDF array if the type is <see cref="PdfValueType.Array"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfArray"/> value, or <c>null</c> if not an array.</returns>
    public static PdfArray AsArray(this IPdfValue value)
    {
        if (value is PdfValue<PdfArray> arrayValue && arrayValue.Type == PdfValueType.Array)
        {
            return arrayValue.Value;
        }
        return null;
    }

    /// <summary>
    /// Returns the value as a PDF dictionary if the type is <see cref="PdfValueType.Dictionary"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfDictionary"/> value, or <c>null</c> if not a dictionary.</returns>
    public static PdfDictionary AsDictionary(this IPdfValue value)
    {
        if (value is PdfValue<PdfDictionary> dictionaryValue && dictionaryValue.Type == PdfValueType.Dictionary)
        {
            return dictionaryValue.Value;
        }
        return null;
    }

    /// <summary>
    /// Resolves a reference value to its underlying non-reference value, following references up to <paramref name="maxDepth"/>.
    /// Returns <c>null</c> if the value is not a reference, the document is <c>null</c>, or the reference cannot be resolved.
    /// </summary>
    /// <param name="value">The PDF value to resolve.</param>
    /// <param name="document">The PDF document used for object resolution.</param>
    /// <param name="maxDepth">Maximum recursion depth for reference resolution.</param>
    /// <returns>The resolved non-reference <see cref="IPdfValue"/>, or <c>null</c> if resolution fails.</returns>
    public static IPdfValue ResolveToNonReference(this IPdfValue value, PdfDocument document, int maxDepth = 10)
    {
        if (value == null || document == null || maxDepth <= 0)
        {
            return null;
        }

        if (value.Type != PdfValueType.Reference)
        {
            return value;
        }

        var reference = (IPdfValue<PdfReference>)value;

        var referencedObject = document.ObjectCache.GetObject(reference.Value);

        if (referencedObject == null)
        {
            return null;
        }

        return ResolveToNonReference(referencedObject.Value, document, maxDepth - 1);
    }

    /// <summary>
    /// Returns the value as a PDF reference if the type is <see cref="PdfValueType.Reference"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfReference"/> value, or <c>null</c> if not a reference.</returns>
    public static PdfReference? AsReference(this IPdfValue value)
    {
        if (value is IPdfValue<PdfReference> referenceValue && referenceValue.Type == PdfValueType.Reference)
        {
            return referenceValue.Value;
        }

        return null;
    }
}