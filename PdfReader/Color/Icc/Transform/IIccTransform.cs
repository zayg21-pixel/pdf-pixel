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
