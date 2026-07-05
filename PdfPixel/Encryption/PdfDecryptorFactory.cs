namespace PdfPixel.Encryption;

/// <summary>
/// Factory selecting appropriate decryptor implementation based on /V and /R.
/// </summary>
public static class PdfDecryptorFactory
{
    /// <summary>
    /// Creates a decryptor for the given parameters based on the /R revision, or <see langword="null"/> for unsupported revisions.
    /// </summary>
    public static BasePdfDecryptor? Create(PdfDecryptorParameters parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        if (parameters.R <= 2)
        {
            return new StandardR2Decryptor(parameters);
        }

        if (parameters.R == 3 || parameters.R == 4)
        {
            return new R3R4Decryptor(parameters);
        }

        if (parameters.R == 5 || parameters.R == 6)
        {
            return new R5R6Decryptor(parameters);
        }

        return null;
    }
}
