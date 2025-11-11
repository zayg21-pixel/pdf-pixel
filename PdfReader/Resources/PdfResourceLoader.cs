using PdfReader.Text;
using System.IO;

namespace PdfReader.Resources
{
    public static class PdfResourceLoader
    {
        /// <summary>
        /// Loads an embedded resource from the assembly as a byte array.
        /// </summary>
        /// <param name="resourceName">Resource name.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static byte[] GetResource(string resourceName)
        {
            var assembly = typeof(PdfTextResourceConverter).Assembly;
            // Open the resource stream
            using Stream stream = assembly.GetManifestResourceStream($"PdfReader.Resources.{resourceName}");

            if (stream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found.");
            }

            using MemoryStream memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
