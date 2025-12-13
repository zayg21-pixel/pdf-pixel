using PdfReader.Color.Icc.Model;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Icc.Transform
{
    internal interface IIccTransform // TODO: cleanup and document
    {
        public void Transform(ref Vector4 color);
    }

    internal class IccChainedTransform : IIccTransform
    {
        private readonly IIccTransform[] _transforms;

        public IccChainedTransform(params IIccTransform[] transforms)
        {
            List<IIccTransform> result = new List<IIccTransform>();

            foreach (var transform in transforms)
            {
                if (transform is IccChainedTransform chainedTransform)
                {
                    result.AddRange(chainedTransform._transforms);
                }
                else
                {
                    result.Add(transform);
                }
            }

            _transforms = result.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(ref Vector4 color)
        {
            ref IIccTransform transformRef = ref _transforms[0];

            for (int i = 0; i < _transforms.Length; i++)
            {
                transformRef.Transform(ref color);
                transformRef = ref Unsafe.Add(ref transformRef, 1);
            }
        }
    }

    internal class IccMatrixTransform : IIccTransform
    {
        public IccMatrixTransform(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        public IccMatrixTransform(float[,] matrix3x3, float[] offset = default, bool transpose = true)
        {
            var matrix4X4 = IccVectorUtilities.ToMatrix4x4(matrix3x3);

            if (transpose)
            {
                matrix4X4 = Matrix4x4.Transpose(matrix4X4);
            }

            if (offset != null && offset.Length >= 3)
            {
                matrix4X4.M41 = offset[0];
                matrix4X4.M42 = offset[1];
                matrix4X4.M43 = offset[2];
            }

            Matrix = matrix4X4;
        }

        public IccMatrixTransform(IccXyz[] components)
        {
            if (components.Length > 4)
            {
                throw new NotSupportedException($"Invalid number of components {components.Length} for matrix transform");
            }

            float m11, m12, m13, m14;

            if (components.Length >= 1)
            {
                m11 = components[0].X;
                m12 = components[0].Y;
                m13 = components[0].Z;
                m14 = 0;
            }
            else
            {
                m11 = 0;
                m12 = 0;
                m13 = 0;
                m14 = 1;
            }

            float m21, m22, m23, m24;

            if (components.Length >= 2)
            {
                m21 = components[1].X;
                m22 = components[1].Y;
                m23 = components[1].Z;
                m24 = 0;
            }
            else
            {
                m21 = 0;
                m22 = 0;
                m23 = 0;
                m24 = 1;
            }

            float m31, m32, m33, m34;

            if (components.Length >= 3)
            {
                m31 = components[2].X;
                m32 = components[2].Y;
                m33 = components[2].Z;
                m34 = 0;
            }
            else
            {
                m31 = 0;
                m32 = 0;
                m33 = 0;
                m34 = 1;
            }

            float m41, m42, m43, m44;

            if (components.Length >= 4)
            {
                m41 = components[3].X;
                m42 = components[3].Y;
                m43 = components[3].Z;
                m44 = 0;
            }
            else
            {
                m41 = 0;
                m42 = 0;
                m43 = 0;
                m44 = 1;
            }

            Matrix = new Matrix4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
        }

        public Matrix4x4 Matrix { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(ref Vector4 color)
        {
            color = Vector4.Transform(color, Matrix);
        }
    }

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

    internal class IccFunctionTransform : IIccTransform
    {
        private readonly Func<Vector4, Vector4> _function;

        public IccFunctionTransform(Func<Vector4, Vector4> function)
        {
            _function = function;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(ref Vector4 color)
        {
            color = _function(color);
        }
    }

    internal static class IccVectorUtilities
    {
        public static Matrix4x4 ToMatrix4x4(float[,] matrix3x3)
        {
            return new Matrix4x4(
                matrix3x3[0, 0], matrix3x3[0, 1], matrix3x3[0, 2], 0,
                matrix3x3[1, 0], matrix3x3[1, 1], matrix3x3[1, 2], 0,
                matrix3x3[2, 0], matrix3x3[2, 1], matrix3x3[2, 2], 0,
                0, 0, 0, 1);
        }

        public static Vector4 ToVector4(ReadOnlySpan<float> data)
        {
            var result = Vector4.One;

            ref var resultRef = ref Unsafe.As<Vector4, float>(ref result);
           
            for (int i = 0; i < data.Length && i < 4; i++)
            {
                resultRef = data[i];
                resultRef = ref Unsafe.Add(ref resultRef, 1);
            }

            return result;
        }
    }
}
