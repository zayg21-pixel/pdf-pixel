namespace PdfPixel.Objects;

/// <summary>
/// Extension methods for extracting strongly-typed values from <see cref="PdfValue"/> instances.
/// Provides conversion and type-checking helpers for PDF value types.
/// </summary>
internal static class PdfValueExtension
{
    /// <summary>
    /// Returns the value as a PDF name if the type is <see cref="PdfNameValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfString"/> representing the name, or <c>null</c> if not a name.</returns>
    public static PdfString? AsName(this PdfValue? value)
    {
        if (value is PdfNameValue nameValue)
        {
            return nameValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as a PDF string if the value carries string content; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfString"/> value, or <c>null</c> if not a string type.</returns>
    public static PdfString? AsString(this PdfValue? value)
    {
        if (value is PdfStringValue stringValue)
        {
            return stringValue.Value;
        }
        else if (value is PdfNameValue nameValue)
        {
            return nameValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as an integer if the type is <see cref="PdfIntegerValue"/> or <see cref="PdfRealValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The integer value, or <c>null</c> if not numeric.</returns>
    public static int? AsInteger(this PdfValue? value)
    {
        if (value is PdfIntegerValue integerValue)
        {
            return integerValue.Value;
        }
        else if (value is PdfRealValue realValue)
        {
            return (int)realValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as a float if the type is <see cref="PdfIntegerValue"/> or <see cref="PdfRealValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The float value, or <c>null</c> if not numeric.</returns>
    public static float? AsFloat(this PdfValue? value)
    {
        if (value is PdfRealValue realValue)
        {
            return realValue.Value;
        }
        else if (value is PdfIntegerValue integerValue)
        {
            return integerValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as a boolean if the type is <see cref="PdfBooleanValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The boolean value, or <c>null</c> if not a boolean.</returns>
    public static bool? AsBoolean(this PdfValue? value)
    {
        if (value is PdfBooleanValue booleanValue)
        {
            return booleanValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as a PDF array if the type is <see cref="PdfArrayValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfArray"/> value, or <c>null</c> if not an array.</returns>
    public static PdfArray? AsArray(this PdfValue? value)
    {
        if (value is PdfArrayValue arrayValue)
        {
            return arrayValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the value as a PDF dictionary if the type is <see cref="PdfDictionaryValue"/>; otherwise returns <c>null</c>.
    /// </summary>
    /// <param name="value">The PDF value to convert.</param>
    /// <returns>The <see cref="PdfDictionary"/> value, or <c>null</c> if not a dictionary.</returns>
    public static PdfDictionary? AsDictionary(this PdfValue? value)
    {
        if (value is PdfDictionaryValue dictionaryValue)
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
    /// <returns>The resolved non-reference <see cref="PdfValue"/>, or <c>null</c> if resolution fails.</returns>
    public static PdfValue? ResolveToNonReference(this PdfValue? value, IPdfDocumentInternal document, int maxDepth = 10)
    {
        if (value == null || document == null || maxDepth <= 0)
        {
            return null;
        }

        if (value is not PdfReferenceValue referenceValue)
        {
            return value;
        }

        PdfObject? referencedObject = document.ObjectCache.GetObject(referenceValue.Value);

        if (referencedObject == null)
        {
            return null;
        }

        return ResolveToNonReference(referencedObject.Value, document, maxDepth - 1);
    }
}
