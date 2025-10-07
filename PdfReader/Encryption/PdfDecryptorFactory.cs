using PdfReader.Models;

namespace PdfReader.Encryption
{
    /// <summary>
    /// Factory selecting appropriate decryptor implementation based on /V and /R.
    /// </summary>
    public static class PdfDecryptorFactory
    {
        public static BasePdfDecryptor Create(PdfDecryptorParameters parameters)
        {
            if (parameters == null)
            {
                return null;
            }
            // Only handle legacy revisions for now
            if (parameters.R <= 2)
            {
                return new StandardR2Decryptor(parameters);
            }
            if (parameters.R == 3 || parameters.R == 4)
            {
                return new StandardR3R4Decryptor(parameters);
            }
            // Future: R5/R6 AES-256 decryptor
            return null;
        }
    }
}
