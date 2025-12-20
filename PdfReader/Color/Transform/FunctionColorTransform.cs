using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    public delegate Vector4 PixelProcessorFunction(Vector4 input);

    internal sealed class FunctionColorTransform : IColorTransform
    {
        private readonly PixelProcessorFunction _function;

        public FunctionColorTransform(PixelProcessorFunction function)
        {
            _function = function;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Transform(Vector4 color)
        {
            return _function(color);
        }

        public PixelProcessorFunction Function => _function;
    }
}
