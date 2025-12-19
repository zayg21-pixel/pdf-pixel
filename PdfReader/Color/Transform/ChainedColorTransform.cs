using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    public delegate void PixelProcessorCallback(ref Vector4 y);

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
        public void Transform(ref Vector4 color)
        {
            ref IColorTransform transformRef = ref _transforms[0];

            for (int i = 0; i < _transforms.Length; i++)
            {
                transformRef.Transform(ref color);
                transformRef = ref Unsafe.Add(ref transformRef, 1);
            }
        }

        public PixelProcessorCallback GetCallback()
        {
            PixelProcessorCallback pixelProcessorCallback = default;

            foreach (var transform in _transforms)
            {
                if (transform is FunctionColorTransform functionTransform)
                {
                    var function = functionTransform.Function;
                    pixelProcessorCallback += function;
                    continue;
                }
                pixelProcessorCallback += transform.Transform;
            }

            return pixelProcessorCallback;
        }
    }
}
