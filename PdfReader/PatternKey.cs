using CommunityToolkit.HighPerformance.Helpers;
using System;

namespace PdfReader
{
    internal readonly struct PatternKey : IEquatable<PatternKey>
    {
        private readonly int _hash;
        private readonly int _length;
        private readonly ulong _firstBytes; // Store first 8 bytes for collision resolution

        public PatternKey(ReadOnlySpan<byte> pattern)
        {
            _length = pattern.Length;
            _hash = HashCode<byte>.Combine(pattern);

            // Store first 8 bytes for collision detection
            _firstBytes = 0;
            int bytesToStore = Math.Min(8, pattern.Length);
            for (int i = 0; i < bytesToStore; i++)
            {
                _firstBytes |= ((ulong)pattern[i]) << (i * 8);
            }
        }

        public bool Equals(PatternKey other)
        {
            return _hash == other._hash &&
                   _length == other._length &&
                   _firstBytes == other._firstBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is PatternKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }
    }
}