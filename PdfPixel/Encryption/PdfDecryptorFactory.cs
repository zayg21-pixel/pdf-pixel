using System;

namespace PdfPixel.Encryption;

/// <summary>
/// Factory selecting appropriate decryptor implementation based on /V and /R.
/// </summary>
public static class PdfDecryptorFactory
{
    /// <summary>
    /// Creates a decryptor for the given parameters based on the /R revision.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown if the /R revision is not supported.</exception>
    public static BasePdfDecryptor Create(PdfDecryptorParameters parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
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

        throw new NotSupportedException($"Unsupported encryption revision (V={parameters.V} R={parameters.R}).");
    }
}
