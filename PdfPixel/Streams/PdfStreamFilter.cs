namespace PdfPixel.Streams;

/// <summary>
/// One filter of a stream's filter chain (/Filter) together with its parsed /DecodeParms entry.
/// </summary>
public readonly struct PdfStreamFilter
{
    /// <summary>
    /// Initializes a new <see cref="PdfStreamFilter"/> from a filter and its decode parameters.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="decodeParameters">The decode parameters of the filter, or null when it has none.</param>
    internal PdfStreamFilter(PdfFilterType filter, PdfDecodeParameters? decodeParameters)
    {
        Filter = filter;
        DecodeParameters = decodeParameters;
    }

    /// <summary>
    /// The filter.
    /// </summary>
    public PdfFilterType Filter { get; }

    /// <summary>
    /// The decode parameters of the filter, or null when it has none.
    /// </summary>
    public PdfDecodeParameters? DecodeParameters { get; }

    /// <summary>
    /// Returns the decode parameters as <typeparamref name="T"/>, or null when the filter has none or they are of another type.
    /// </summary>
    /// <typeparam name="T">The expected parameters type.</typeparam>
    public T? GetParameters<T>()
        where T : PdfDecodeParameters
    {
        return DecodeParameters as T;
    }
}
