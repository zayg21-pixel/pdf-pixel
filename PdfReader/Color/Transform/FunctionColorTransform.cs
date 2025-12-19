using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform
{
    internal sealed class FunctionColorTransform : IColorTransform
    {
        private readonly PixelProcessorCallback _function;

        public FunctionColorTransform(PixelProcessorCallback function)
        {
            _function = function;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(ref Vector4 color)
        {
            _function(ref color);
        }

        public PixelProcessorCallback Function => _function;
    }
}
