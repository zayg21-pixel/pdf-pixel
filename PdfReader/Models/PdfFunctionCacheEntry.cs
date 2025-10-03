using System;

namespace PdfReader.Models
{
    /// <summary>
    /// Holds decoded sample table and metadata for a sampled (Type 0) PDF function.
    /// This avoids re-reading and bit-unpacking the function stream for repeated evaluations.
    /// </summary>
    internal sealed class PdfFunctionCacheEntry
    {
        public PdfFunctionCacheEntry(int[] sizes, int components, int bitsPerSample, float[] table, int[] strides, float[] rangePairs, float[] decodePairs, float[] encodePairs)
        {
            Sizes = sizes;
            ComponentCount = components;
            BitsPerSample = bitsPerSample;
            Table = table;
            Strides = strides;
            RangePairs = rangePairs;
            DecodePairs = decodePairs;
            EncodePairs = encodePairs;
        }

        public int[] Sizes { get; }
        public int ComponentCount { get; }
        public int BitsPerSample { get; }
        public float[] Table { get; }
        public int[] Strides { get; }
        public float[] RangePairs { get; } // [min0,max0,min1,max1,...]
        public float[] DecodePairs { get; } // optional, may be null
        public float[] EncodePairs { get; } // optional, may be null
    }
}
