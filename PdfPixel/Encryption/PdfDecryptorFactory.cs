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
    /// <param name="onCredentialRequested">Called when the empty user password does not authenticate, or null to try only the empty password.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown if the /Filter security handler, the /V algorithm or the /R revision is not supported.</exception>
    public static BasePdfDecryptor Create(PdfDecryptorParameters parameters, PdfCredentialRequestedCallback? onCredentialRequested)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (parameters.Filter != PdfSecurityHandler.Standard)
        {
            throw new NotSupportedException("Unsupported security handler; only the Standard security handler is supported.");
        }

        if (parameters.V != 1 && parameters.V != 2 && parameters.V != 4 && parameters.V != 5)
        {
            throw new NotSupportedException($"Unsupported encryption algorithm (V={parameters.V} R={parameters.R}).");
        }

        if (parameters.R <= 2)
        {
            return new R2Decryptor(parameters, onCredentialRequested);
        }

        if (parameters.R == 3 || parameters.R == 4)
        {
            return new R3R4Decryptor(parameters, onCredentialRequested);
        }

        if (parameters.R == 5 || parameters.R == 6)
        {
            return new R5R6Decryptor(parameters, onCredentialRequested);
        }

        throw new NotSupportedException($"Unsupported encryption revision (V={parameters.V} R={parameters.R}).");
    }
}
