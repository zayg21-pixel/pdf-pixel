using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfPixel.PostScript.Tokens
{
    public class CaseInsensitiveGetterDictionary<TValue> : IDictionary<string, TValue>
    {
        private readonly Dictionary<string, TValue> _dictionary;

        public CaseInsensitiveGetterDictionary()
        {
            _dictionary = new Dictionary<string, TValue>();
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key. Retrieval prioritizes case-sensitive match, then falls back to case-insensitive scan.
        /// </summary>
        public TValue this[string key]
        {
            get
            {
                if (_dictionary.TryGetValue(key, out TValue value))
                {
                    return value;
                }
                foreach (var pair in _dictionary)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
                throw new KeyNotFoundException();
            }
            set
            {
                _dictionary[key] = value;
            }
        }

        public ICollection<string> Keys => _dictionary.Keys;

        public ICollection<TValue> Values => _dictionary.Values;

        public int Count => _dictionary.Count;

        public bool IsReadOnly => false;

        public void Add(string key, TValue value)
        {
            _dictionary.Add(key, value);
        }

        public void Add(KeyValuePair<string, TValue> item)
        {
            _dictionary.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<string, TValue> item)
        {
            return ((IDictionary<string, TValue>)_dictionary).Contains(item);
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key. Prioritizes case-sensitive match, then falls back to case-insensitive scan.
        /// </summary>
        public bool ContainsKey(string key)
        {
            if (_dictionary.ContainsKey(key))
            {
                return true;
            }
            foreach (var dictKey in _dictionary.Keys)
            {
                if (string.Equals(dictKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<string, TValue>)_dictionary).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public bool Remove(string key)
        {
            return _dictionary.Remove(key);
        }

        public bool Remove(KeyValuePair<string, TValue> item)
        {
            return ((IDictionary<string, TValue>)_dictionary).Remove(item);
        }

        /// <summary>
        /// Gets the value associated with the specified key. Retrieval prioritizes case-sensitive match, then falls back to case-insensitive scan.
        /// </summary>
        public bool TryGetValue(string key, out TValue value)
        {
            if (_dictionary.TryGetValue(key, out value))
            {
                return true;
            }
            foreach (var pair in _dictionary)
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
