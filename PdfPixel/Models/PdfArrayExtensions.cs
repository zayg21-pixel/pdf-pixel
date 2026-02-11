using System;

namespace PdfPixel.Models;

/// <summary>
/// Extension helpers for <see cref="PdfArray"/> providing strongly-typed
/// bulk conversions commonly needed by higher level PDF model code.
/// </summary>
public static class PdfArrayExtensions
{
    /// <summary>
    /// Convert the contents of a <see cref="PdfArray"/> to a <see cref="float"/> array.
    /// Non-numeric entries are converted using the same numeric coercion rules as
    /// <see cref="PdfArray.GetFloatOrDefault(int)"/> (yielding 0 for incompatible types).
    /// Returns an empty array when <paramref name="array"/> is null or has no items.
    /// </summary>
    /// <param name="array">Source PDF array.</param>
    /// <returns>New float array (never null).</returns>
    public static float[] GetFloatArray(this PdfArray array)
    {
        if (array == null || array.Count == 0)
        {
            return Array.Empty<float>();
        }

        int count = array.Count;
        float[] result = new float[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = array.GetFloatOrDefault(index);
        }
        return result;
    }

    /// <summary>
    /// Convert the contents of a <see cref="PdfArray"/> to an <see cref="int"/> array.
    /// Each element is obtained via <see cref="PdfArray.GetIntegerOrDefault(int)"/>, which coerces
    /// real numbers by truncation and yields 0 for incompatible types. The length of the
    /// returned array always matches the source array length. Returns an empty array when
    /// <paramref name="array"/> is null or empty.
    /// </summary>
    /// <param name="array">Source PDF array.</param>
    /// <returns>New int array (never null).</returns>
    public static int[] GetIntegerArray(this PdfArray array)
    {
        if (array == null || array.Count == 0)
        {
            return Array.Empty<int>();
        }

        int count = array.Count;
        int[] result = new int[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = array.GetIntegerOrDefault(index);
        }
        return result;
    }
}
