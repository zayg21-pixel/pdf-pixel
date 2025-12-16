using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Transform
{
    internal class IccClutTransform : IIccTransform
    {
        private readonly float[] _clut;
        private readonly int _outChannels;
        private readonly int[] _gridPointsPerDimension;

        public IccClutTransform(float[] clut, int outChannels, int[] gridPointsPerDimension)
        {
            _clut = clut;
            _outChannels = outChannels;
            _gridPointsPerDimension = gridPointsPerDimension;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(ref Vector4 color)
        {
            float[] vin = [color.X, color.Y, color.Z, color.W];
            float[] vout = EvaluateClutLinearCore(_clut, _outChannels, _gridPointsPerDimension, vin);

            ref float colorRef = ref Unsafe.As<Vector4, float>(ref color);

            for (int i = 0; i < 4; i++)
            {
                if (i >= vout.Length)
                {
                    colorRef = 1;
                    continue;
                }
                colorRef = vout[i];
                colorRef = ref Unsafe.Add(ref colorRef, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float[] EvaluateClutLinearCore(float[] clut, int outChannels, int[] gridPointsPerDimension, float[] vin)
        {
            int dimensionCount = gridPointsPerDimension.Length;
            if (dimensionCount != vin.Length)
            {
                // Clamp to the minimum safe dimension count
                dimensionCount = Math.Min(dimensionCount, vin.Length);
            }

            // Pre-allocate index and fraction arrays (stack alloc would need spans; keep simple per rules)
            var index0 = new int[dimensionCount];
            var fraction = new float[dimensionCount];

            // Clamp, scale and decompose positions
            for (int d = 0; d < dimensionCount; d++)
            {
                int grid = gridPointsPerDimension[d];
                if (grid <= 1)
                {
                    index0[d] = 0;
                    fraction[d] = 0f;
                    continue;
                }

                float scale = grid - 1;
                float p = vin[d] * scale;
                if (p < 0f)
                {
                    p = 0f;
                }
                else if (p > scale)
                {
                    p = scale;
                }

                int i0 = (int)p; // floor since p >= 0
                float f = p - i0;
                index0[d] = i0;
                fraction[d] = f;
            }

            // Compute strides per dimension (innermost dimension is last index in loops => reverse order)
            var stride = new int[dimensionCount];
            int cumulative = outChannels;
            for (int d = dimensionCount - 1; d >= 0; d--)
            {
                stride[d] = cumulative;
                int grid = gridPointsPerDimension[d];
                cumulative *= grid;
            }

            var result = new float[outChannels];
            int vertexCount = 1 << dimensionCount;

            for (int vertexMask = 0; vertexMask < vertexCount; vertexMask++)
            {
                float weight = 1f;
                int offset = 0;

                for (int d = 0; d < dimensionCount; d++)
                {
                    int grid = gridPointsPerDimension[d];
                    int bit = vertexMask >> d & 1;
                    int idx = index0[d] + bit;
                    if (idx >= grid)
                    {
                        weight = 0f;
                        break;
                    }

                    float f = fraction[d];
                    weight *= bit == 0 ? 1f - f : f;
                    offset += idx * stride[d];
                }

                if (weight == 0f)
                {
                    continue;
                }

                int baseIndex = offset;
                for (int c = 0; c < outChannels; c++)
                {
                    result[c] += clut[baseIndex + c] * weight;
                }
            }

            return result;
        }
    }
}
