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
    /// <param name="parameters">Encryption parameters of the document.</param>
    /// <param name="onPasswordRequested">Called when the empty user password does not authenticate, or null to try only the empty password.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown if the /R revision is not supported.</exception>
    public static BasePdfDecryptor Create(PdfDecryptorParameters parameters, PdfPasswordRequestedCallback? onPasswordRequested)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (parameters.R <= 2)
        {
            return new StandardR2Decryptor(parameters, onPasswordRequested);
        }

        if (parameters.R == 3 || parameters.R == 4)
        {
            return new R3R4Decryptor(parameters, onPasswordRequested);
        }

        if (parameters.R == 5 || parameters.R == 6)
        {
            return new R5R6Decryptor(parameters, onPasswordRequested);
        }

        throw new NotSupportedException($"Unsupported encryption revision (V={parameters.V} R={parameters.R}).");
    }
}
