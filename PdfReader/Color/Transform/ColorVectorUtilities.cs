using PdfReader.Color.Structures;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    internal static class ColorVectorUtilities
    {
        private static readonly Vector4 MaxByte = new Vector4(255f);
        private static readonly Vector4 ByteOffset = new Vector4(0.5f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 ToMatrix4x4(float[,] matrix3x3)
        {
            return new Matrix4x4(
                matrix3x3[0, 0], matrix3x3[0, 1], matrix3x3[0, 2], 0,
                matrix3x3[1, 0], matrix3x3[1, 1], matrix3x3[1, 2], 0,
                matrix3x3[2, 0], matrix3x3[2, 1], matrix3x3[2, 2], 0,
                0, 0, 0, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToVector4WithOnePadding(ReadOnlySpan<float> data)
        {
            switch (data.Length)
            {
                case 0:
                    return Vector4.One;
                case 1:
                    return new Vector4(data[0], 1, 1, 1);
                case 2:
                    return new Vector4(data[0], data[1], 1, 1);

                case 3:
                    return new Vector4(data[0], data[1], data[2], 1);
                default:
                    return new Vector4(data[0], data[1], data[2], data[3]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Load01ToRgba(Vector4 source, ref RgbaPacked destination)
        {
            var scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;

            destination.R = (byte)scaled.X;
            destination.G = (byte)scaled.Y;
            destination.B = (byte)scaled.Z;
            destination.A = 255;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 ToVector4WithZeroPadding(ReadOnlySpan<float> data)
        {
            var result = Vector4.Zero;

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
