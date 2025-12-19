using System.Numerics;

namespace PdfReader.Color.Transform
{
    internal interface IColorTransform // TODO: cleanup and document
    {
        public void Transform(ref Vector4 color);
    }
}
