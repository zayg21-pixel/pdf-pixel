using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    internal sealed class ChainedColorTransform : IColorTransform
    {
        private readonly IColorTransform[] _transforms;

        public ChainedColorTransform(params IColorTransform[] transforms)
        {
            List<IColorTransform> result = new List<IColorTransform>();

            foreach (var transform in transforms)
            {
                if (transform is ChainedColorTransform chainedTransform)
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
        public Vector4 Transform(Vector4 color)
        {
            if (_transforms.Length == 0)
            {
                return color;
            }

            ref IColorTransform currentTransform = ref _transforms[0];
            for (int i = 0; i < _transforms.Length; i++)
            {
                color = currentTransform.Transform(color);

                if (i != _transforms.Length - 1)
                {
                    currentTransform = ref Unsafe.Add(ref currentTransform, 1);
                }
            }

            return color;
        }
    }
}
