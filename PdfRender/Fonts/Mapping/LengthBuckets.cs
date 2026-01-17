using System.Collections.Generic;

namespace PdfRender.Fonts.Mapping;

/// <summary>
/// Holds lists of items partitioned by code byte length (1..4).
/// Provides indexer access to the list for a given length and exposes
/// MinLength/MaxLength constants to avoid magic numbers.
/// </summary>
internal sealed class LengthBuckets<T>
{
    /// <summary>
    /// Minimum supported code byte length.
    /// </summary>
    public const int MinLength = 1;

    /// <summary>
    /// Maximum supported code byte length.
    /// </summary>
    public const int MaxLength = 4;

    private readonly List<T>[] buckets = new List<T>[MaxLength + 1];

    /// <summary>
    /// Initializes buckets for each supported length.
    /// </summary>
    public LengthBuckets()
    {
        buckets[1] = new List<T>();
        buckets[2] = new List<T>();
        buckets[3] = new List<T>();
        buckets[4] = new List<T>();
    }

    /// <summary>
    /// Gets the list for the specified length or null if out of range.
    /// </summary>
    public List<T> this[int length]
    {
        get
        {
            if (length < MinLength || length > MaxLength)
            {
                return null;
            }
            return buckets[length];
        }
    }
}
