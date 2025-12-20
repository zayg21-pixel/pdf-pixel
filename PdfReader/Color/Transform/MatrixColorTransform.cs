using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    internal sealed class MatrixColorTransform : IColorTransform
    {
        public MatrixColorTransform(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        public MatrixColorTransform(float[,] matrix3x3, float[] offset = default, bool transpose = true)
        {
            var matrix4X4 = ColorVectorUtilities.ToMatrix4x4(matrix3x3);

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

        public MatrixColorTransform(IccXyz[] components)
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

        public MatrixColorTransform()
        {
        }

        public Matrix4x4 Matrix { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Transform(Vector4 color)
        {
            return Vector4.Transform(color, Matrix);
        }
    }
}
