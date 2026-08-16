using PdfPixel.Models;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// A destination as written at the place it is used: the name of a named destination, a reference to
/// the object holding it, or — when written in place — the destination parsed straight away, so that
/// no destination array is ever held on to.
/// </summary>
internal readonly struct PdfDestinationReference
{
    private PdfDestinationReference(PdfString? name, in PdfReference reference, PdfDestination? destination)
    {
        Name = name;
        Reference = reference;
        Destination = destination;
    }

    /// <summary>
    /// Name of one of the document's named destinations. Null unless the destination is named.
    /// </summary>
    public PdfString? Name { get; }

    /// <summary>
    /// Reference to the object holding the destination. Invalid unless the destination is indirect.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// The destination written in place. Null unless it is written in place.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Reads the destination a dictionary holds under a key, resolving nothing it points at.
    /// </summary>
    public static PdfDestinationReference FromDictionary(PdfDictionary dictionary, in PdfString key)
    {
        if (dictionary == null)
        {
            return default;
        }

        PdfReference? reference = dictionary.GetReference(key);
        if (reference != null)
        {
            return new PdfDestinationReference(null, reference.Value, null);
        }

        PdfArray? destinationArray = dictionary.GetArray(key);
        if (destinationArray != null)
        {
            return FromArray(destinationArray);
        }

        PdfString? name = dictionary.GetString(key);
        if (name != null)
        {
            return new PdfDestinationReference(name, default, null);
        }

        return default;
    }

    /// <summary>
    /// Reads the destination an array holds at an index, resolving nothing it points at.
    /// </summary>
    public static PdfDestinationReference FromArray(PdfArray array, int index)
    {
        if (array == null)
        {
            return default;
        }

        PdfReference? reference = array.GetReference(index);
        if (reference != null)
        {
            return new PdfDestinationReference(null, reference.Value, null);
        }

        PdfArray? destinationArray = array.GetArray(index);
        if (destinationArray != null)
        {
            return FromArray(destinationArray);
        }

        PdfString? name = array.GetString(index);
        if (name != null)
        {
            return new PdfDestinationReference(name, default, null);
        }

        return default;
    }

    private static PdfDestinationReference FromArray(PdfArray destinationArray)
    {
        if (destinationArray.Count == 0)
        {
            return default;
        }

        return new PdfDestinationReference(null, default, new PdfDestination(destinationArray));
    }
}
