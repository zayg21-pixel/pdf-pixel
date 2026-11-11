using PdfPixel.Functions;
using PdfPixel.Objects;

namespace PdfPixel.Rendering;

/// <summary>
/// A transfer function (TR): one function each for red, green, blue and gray, or their complements cyan,
/// magenta, yellow and black.
/// </summary>
public sealed class PdfTransferFunction
{
    private const int FunctionCount = 4;

    private PdfTransferFunction(PdfFunction[] functions) => Functions = functions;

    /// <summary>
    /// Gets the functions for red, green, blue and gray, or cyan, magenta, yellow and black, in that order.
    /// Each function takes and returns a component in additive form.
    /// </summary>
    public PdfFunction[] Functions { get; }

    /// <summary>
    /// Returns the transfer function held by <paramref name="key"/> of <paramref name="dictionary"/>: a single
    /// function, which applies to every component, or an array of four functions.
    /// </summary>
    /// <param name="dictionary">Dictionary holding the TR entry.</param>
    /// <param name="key">Key of the TR entry.</param>
    /// <returns>The transfer function, or null for absent or invalid input.</returns>
    internal static PdfTransferFunction? GetTransferFunction(PdfDictionary dictionary, in PdfString key)
        => dictionary.Document.ObjectCache.GetOrCreate(dictionary, key, FromObject);

    private static PdfTransferFunction? FromObject(PdfObject functionObject)
    {
        PdfValue value = functionObject.Value;

        if (value is PdfArrayValue)
        {
            return FromArray(value.AsArray());
        }
        else
        {
            return FromFunction(PdfFunctions.FromObject(functionObject));
        }
    }

    private static PdfTransferFunction? FromArray(PdfArray? array)
    {
        if (array == null || array.Count != FunctionCount)
        {
            return null;
        }

        var functions = new PdfFunction[FunctionCount];

        for (int index = 0; index < FunctionCount; index++)
        {
            PdfFunction? function = PdfFunctions.GetFunction(array, index);

            if (function == null)
            {
                return null;
            }

            functions[index] = function;
        }

        return new PdfTransferFunction(functions);
    }

    private static PdfTransferFunction? FromFunction(PdfFunction? function)
    {
        if (function == null)
        {
            return null;
        }

        return new PdfTransferFunction([function, function, function, function]);
    }
}
