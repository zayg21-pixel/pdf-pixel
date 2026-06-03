using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// An <see cref="IDictionary{TKey, TValue}"/> wrapper that stores entries with their original casing
/// but performs case-insensitive fallback on all lookup operations (indexer, TryGetValue, ContainsKey).
/// Writes always use the exact key supplied; the case-insensitive scan is used only when an exact match
/// is not found.
/// </summary>
/// <typeparam name="TValue">Type of the dictionary values.</typeparam>
public class CaseInsensitiveGetterDictionary<TValue> : IDictionary<string, TValue>
{
    private readonly Dictionary<string, TValue> _dictionary;

    /// <summary>
    /// Initializes an empty case-insensitive getter dictionary.
    /// </summary>
    public CaseInsensitiveGetterDictionary() => _dictionary = new Dictionary<string, TValue>();

    /// <summary>
    /// Gets or sets the value associated with the specified key. Retrieval prioritizes case-sensitive match, then falls back to case-insensitive scan.
    /// </summary>
    public TValue this[string key]
    {
        get
        {
            if (_dictionary.TryGetValue(key, out TValue? value))
            {
                return value;
            }

            foreach (KeyValuePair<string, TValue> pair in _dictionary)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            throw new KeyNotFoundException();
        }

        set => _dictionary[key] = value;
    }

    /// <summary>
    /// Gets a collection containing the keys in the dictionary, in insertion order.
    /// </summary>
    public ICollection<string> Keys => _dictionary.Keys;

    /// <summary>
    /// Gets a collection containing the values in the dictionary, in insertion order.
    /// </summary>
    public ICollection<TValue> Values => _dictionary.Values;

    /// <summary>
    /// Gets the number of key-value pairs currently stored in the dictionary.
    /// </summary>
    public int Count => _dictionary.Count;

    /// <summary>
    /// Always returns false; this dictionary is mutable.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds the specified key and value to the dictionary using the exact casing of <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to add. Must not already exist (exact-case match).</param>
    /// <param name="value">The value to associate with the key.</param>
    public void Add(string key, TValue value) => _dictionary.Add(key, value);

    /// <summary>
    /// Adds the key-value pair to the dictionary using the exact casing of the pair's key.
    /// </summary>
    /// <param name="item">The key-value pair to add.</param>
    public void Add(KeyValuePair<string, TValue> item) => _dictionary.Add(item.Key, item.Value);

    /// <summary>
    /// Removes all key-value pairs from the dictionary.
    /// </summary>
    public void Clear() => _dictionary.Clear();

    /// <summary>
    /// Determines whether the dictionary contains the specified key-value pair using an exact-case key comparison.
    /// </summary>
    /// <param name="item">The key-value pair to locate.</param>
    /// <returns>True if the pair is found; otherwise false.</returns>
    public bool Contains(KeyValuePair<string, TValue> item) => ((IDictionary<string, TValue>)_dictionary).Contains(item);

    /// <summary>
    /// Determines whether the dictionary contains the specified key. Prioritizes case-sensitive match, then falls back to case-insensitive scan.
    /// </summary>
    public bool ContainsKey(string key)
    {
        if (_dictionary.ContainsKey(key))
        {
            return true;
        }

        foreach (string dictKey in _dictionary.Keys)
        {
            if (string.Equals(dictKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Copies all key-value pairs to the specified array starting at <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex) => ((IDictionary<string, TValue>)_dictionary).CopyTo(array, arrayIndex);

    /// <summary>
    /// Returns an enumerator that iterates over all key-value pairs in the dictionary.
    /// </summary>
    /// <returns>An enumerator for the dictionary's key-value pairs.</returns>
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

    /// <summary>
    /// Removes the entry with the specified key using an exact-case match.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed; otherwise false.</returns>
    public bool Remove(string key) => _dictionary.Remove(key);

    /// <summary>
    /// Removes the specified key-value pair from the dictionary using an exact-case key match.
    /// </summary>
    /// <param name="item">The key-value pair to remove.</param>
    /// <returns>True if the pair was found and removed; otherwise false.</returns>
    public bool Remove(KeyValuePair<string, TValue> item) => ((IDictionary<string, TValue>)_dictionary).Remove(item);

    /// <summary>
    /// Gets the value associated with the specified key. Retrieval prioritizes case-sensitive match, then falls back to case-insensitive scan.
    /// </summary>
#pragma warning disable CS8767
    public bool TryGetValue(string key, out TValue? value)
#pragma warning restore CS8767
    {
        if (_dictionary.TryGetValue(key, out TValue? dictionaryValue))
        {
            value = dictionaryValue;
            return true;
        }

        foreach (KeyValuePair<string, TValue> pair in _dictionary)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
