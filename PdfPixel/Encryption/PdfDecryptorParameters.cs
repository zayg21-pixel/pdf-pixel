using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Encryption;

/// <summary>
/// Aggregates values from the /Encrypt dictionary and the trailer /ID array required to initialize a decryptor.
/// Holds only extracted scalar values; decryptor implementations can still access the raw dictionary via SourceDictionary
/// to parse advanced or version-specific fields.
/// </summary>
public class PdfDecryptorParameters
{
    /// <summary>
    /// Encryption algorithm version (/V).
    /// </summary>
    public int V { get; set; }

    /// <summary>
    /// Security handler revision (/R).
    /// </summary>
    public int R { get; set; }

    /// <summary>
    /// Key length in bits (/Length). May be 0 if absent (defaults apply later).
    /// </summary>
    public int LengthBits { get; set; }

    /// <summary>
    /// Permissions (/P) raw integer.
    /// </summary>
    public int Permissions { get; set; }

    /// <summary>
    /// Indicates if metadata is encrypted (/EncryptMetadata, default true when absent).
    /// </summary>
    public bool EncryptMetadata { get; set; } = true;

    /// <summary>
    /// First file identifier (/ID[0]).
    /// </summary>
    public byte[]? FileIdFirst { get; set; }

    /// <summary>
    /// Second file identifier (/ID[1]).
    /// </summary>
    public byte[]? FileIdSecond { get; set; }

    /// <summary>
    /// Owner password entry (/O) raw bytes (32 for R&lt;=4, 48 for R&gt;=5).
    /// </summary>
    public byte[]? OwnerEntry { get; set; }

    /// <summary>
    /// User password entry (/U) raw bytes (32 for R&lt;=4, 48 for R&gt;=5).
    /// </summary>
    public byte[]? UserEntry { get; set; }

    /// <summary>
    /// Encrypted file key using owner password (/OE) - R>=5.
    /// </summary>
    public byte[]? OwnerEncryptedKey { get; set; }

    /// <summary>
    /// Encrypted file key using user password (/UE) - R>=5.
    /// </summary>
    public byte[]? UserEncryptedKey { get; set; }

    /// <summary>
    /// Permissions block (/Perms) - R>=5.
    /// </summary>
    public byte[]? Perms { get; set; }

    /// <summary>
    /// Crypt filters declared in the /CF dictionary, keyed by name.
    /// </summary>
    public Dictionary<PdfString, PdfCryptFilter> CryptFilters { get; } = [];

    /// <summary>
    /// Crypt filter applied to streams (/StmF).
    /// </summary>
    public PdfCryptFilter StreamCryptFilter { get; set; } = PdfCryptFilter.Identity;

    /// <summary>
    /// Crypt filter applied to strings (/StrF).
    /// </summary>
    public PdfCryptFilter StringCryptFilter { get; set; } = PdfCryptFilter.Identity;

    /// <summary>
    /// Crypt filter applied to embedded file streams (/EFF).
    /// </summary>
    public PdfCryptFilter EmbeddedFileCryptFilter { get; set; } = PdfCryptFilter.Identity;

    /// <summary>
    /// Returns the crypt filter declared under <paramref name="name"/>, or <see cref="PdfCryptFilter.Identity"/>
    /// when the name is absent, is Identity, or is not declared.
    /// </summary>
    public PdfCryptFilter GetCryptFilter(PdfString? name)
    {
        if (name == null || name.Value == PdfTokens.IdentityKey)
        {
            return PdfCryptFilter.Identity;
        }

        if (CryptFilters.TryGetValue(name.Value, out PdfCryptFilter? cryptFilter))
        {
            return cryptFilter;
        }

        return PdfCryptFilter.Identity;
    }
}
