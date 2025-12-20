using System.Numerics;

namespace PdfReader.Color.Transform
{
    internal interface IColorTransform // TODO: cleanup and document
    {
        public Vector4 Transform(Vector4 color);
    }
}
