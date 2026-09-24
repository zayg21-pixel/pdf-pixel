namespace PdfPixel.Encryption;

/// <summary>
/// Crypt filter from the /CF dictionary of an encrypted document.
/// </summary>
public sealed class PdfCryptFilter
{
    /// <summary>
    /// Initializes a crypt filter with its method, key length and authentication event.
    /// </summary>
    public PdfCryptFilter(PdfCryptFilterMethod method, int? length, PdfAuthEvent authEvent)
    {
        Method = method;
        Length = length;
        AuthEvent = authEvent;
    }

    /// <summary>
    /// The Identity crypt filter, which leaves data unchanged.
    /// </summary>
    public static PdfCryptFilter Identity { get; } = new(PdfCryptFilterMethod.None, null, PdfAuthEvent.DocumentOpen);

    /// <summary>
    /// Decryption method (/CFM).
    /// </summary>
    public PdfCryptFilterMethod Method { get; }

    /// <summary>
    /// Key length (/Length) as stored in the crypt filter dictionary, or null when absent.
    /// </summary>
    public int? Length { get; }

    /// <summary>
    /// Event requiring authentication before this filter decrypts (/AuthEvent).
    /// </summary>
    public PdfAuthEvent AuthEvent { get; }
}
