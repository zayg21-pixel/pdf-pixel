using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Encryption.Decryption;

/// <summary>
/// Aggregates values from the /Encrypt dictionary and the trailer /ID array required to initialize a decryptor.
/// Holds only extracted scalar values; decryptor implementations can still access the raw dictionary via SourceDictionary
/// to parse advanced or version-specific fields.
/// </summary>
public class PdfDecryptorParameters
{
    /// <summary>
    /// Reads the parameters of <paramref name="encryptDictionary"/> and the trailer file identifier <paramref name="fileId"/>.
    /// </summary>
    /// <param name="encryptDictionary">The /Encrypt dictionary.</param>
    /// <param name="fileId">The trailer /ID array, or <see langword="null"/> when absent.</param>
    internal PdfDecryptorParameters(PdfDictionary encryptDictionary, PdfArray? fileId)
    {
        V = encryptDictionary.GetIntegerOrDefault(PdfTokens.VKey);
        R = encryptDictionary.GetIntegerOrDefault(PdfTokens.RKey);
        LengthBits = encryptDictionary.GetIntegerOrDefault(PdfTokens.LengthKey);
        Permissions = encryptDictionary.GetIntegerOrDefault(PdfTokens.PKey);

        PdfString? filter = encryptDictionary.GetName(PdfTokens.FilterKey);
        Filter = (filter.HasValue) ? filter.Value.AsEnum<PdfSecurityHandler>() : PdfSecurityHandler.Standard;
        EncryptMetadata = encryptDictionary.GetBoolean(PdfTokens.EncryptMetadataKey) ?? true;

        OwnerEntry = encryptDictionary.GetValue(PdfTokens.OKey).AsString()?.Value.ToArray();
        UserEntry = encryptDictionary.GetValue(PdfTokens.UKey).AsString()?.Value.ToArray();

        if (R >= 5)
        {
            OwnerEncryptedKey = encryptDictionary.GetValue(PdfTokens.OEKey).AsString()?.Value.ToArray();
            UserEncryptedKey = encryptDictionary.GetValue(PdfTokens.UEKey).AsString()?.Value.ToArray();
            Perms = encryptDictionary.GetValue(PdfTokens.PermsKey).AsString()?.Value.ToArray();
        }

        if (V >= 4)
        {
            CryptFilters = ReadCryptFilters(encryptDictionary.GetDictionary(PdfTokens.CFKey));

            // StmF and StrF always authenticate on document open (ISO 32000-2, Table 25, AuthEvent).
            PdfCryptFilter streamCryptFilter = GetCryptFilter(encryptDictionary.GetName(PdfTokens.StmFKey));
            PdfCryptFilter stringCryptFilter = GetCryptFilter(encryptDictionary.GetName(PdfTokens.StrFKey));
            StreamCryptFilter = new PdfCryptFilter(streamCryptFilter.Method, streamCryptFilter.Length, PdfAuthEvent.DocumentOpen);
            StringCryptFilter = new PdfCryptFilter(stringCryptFilter.Method, stringCryptFilter.Length, PdfAuthEvent.DocumentOpen);

            PdfString? embeddedFileFilterName = encryptDictionary.GetName(PdfTokens.EffKey);
            if (embeddedFileFilterName == null)
            {
                EmbeddedFileCryptFilter = StreamCryptFilter;
            }
            else
            {
                EmbeddedFileCryptFilter = GetCryptFilter(embeddedFileFilterName);
            }
        }
        else
        {
            CryptFilters = new Dictionary<PdfString, PdfCryptFilter>();

            PdfCryptFilter handlerCryptFilter = new(PdfCryptFilterMethod.V2, null, PdfAuthEvent.DocumentOpen);
            StreamCryptFilter = handlerCryptFilter;
            StringCryptFilter = handlerCryptFilter;
            EmbeddedFileCryptFilter = handlerCryptFilter;
        }

        if (fileId?.Count >= 2)
        {
            FileIdFirst = fileId.GetValue(0).AsString()?.Value.ToArray();
            FileIdSecond = fileId.GetValue(1).AsString()?.Value.ToArray();
        }
    }

    /// <summary>
    /// Security handler (/Filter), Standard when absent.
    /// </summary>
    public PdfSecurityHandler Filter { get; }

    /// <summary>
    /// Encryption algorithm version (/V).
    /// </summary>
    public int V { get; }

    /// <summary>
    /// Security handler revision (/R).
    /// </summary>
    public int R { get; }

    /// <summary>
    /// Key length in bits (/Length). May be 0 if absent (defaults apply later).
    /// </summary>
    public int LengthBits { get; }

    /// <summary>
    /// Permissions (/P) raw integer.
    /// </summary>
    public int Permissions { get; }

    /// <summary>
    /// Indicates if metadata is encrypted (/EncryptMetadata, default true when absent).
    /// </summary>
    public bool EncryptMetadata { get; }

    /// <summary>
    /// First file identifier (/ID[0]).
    /// </summary>
    public byte[]? FileIdFirst { get; }

    /// <summary>
    /// Second file identifier (/ID[1]).
    /// </summary>
    public byte[]? FileIdSecond { get; }

    /// <summary>
    /// Owner password entry (/O) raw bytes (32 for R&lt;=4, 48 for R&gt;=5).
    /// </summary>
    public byte[]? OwnerEntry { get; }

    /// <summary>
    /// User password entry (/U) raw bytes (32 for R&lt;=4, 48 for R&gt;=5).
    /// </summary>
    public byte[]? UserEntry { get; }

    /// <summary>
    /// Encrypted file key using owner password (/OE) - R>=5.
    /// </summary>
    public byte[]? OwnerEncryptedKey { get; }

    /// <summary>
    /// Encrypted file key using user password (/UE) - R>=5.
    /// </summary>
    public byte[]? UserEncryptedKey { get; }

    /// <summary>
    /// Permissions block (/Perms) - R>=5.
    /// </summary>
    public byte[]? Perms { get; }

    /// <summary>
    /// Crypt filters declared in the /CF dictionary, keyed by name.
    /// </summary>
    public IReadOnlyDictionary<PdfString, PdfCryptFilter> CryptFilters { get; }

    /// <summary>
    /// Crypt filter applied to streams (/StmF).
    /// </summary>
    public PdfCryptFilter StreamCryptFilter { get; }

    /// <summary>
    /// Crypt filter applied to strings (/StrF).
    /// </summary>
    public PdfCryptFilter StringCryptFilter { get; }

    /// <summary>
    /// Crypt filter applied to embedded file streams (/EFF).
    /// </summary>
    public PdfCryptFilter EmbeddedFileCryptFilter { get; }

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

    private static Dictionary<PdfString, PdfCryptFilter> ReadCryptFilters(PdfDictionary? cryptFilterDictionary)
    {
        Dictionary<PdfString, PdfCryptFilter> cryptFilters = [];
        if (cryptFilterDictionary == null)
        {
            return cryptFilters;
        }

        foreach (PdfString name in cryptFilterDictionary.RawValues.Keys)
        {
            PdfDictionary? entry = cryptFilterDictionary.GetDictionary(name);
            if (entry == null)
            {
                continue;
            }

            PdfCryptFilter cryptFilter = new(
                entry.GetNameOrDefault(PdfTokens.CfmKey).AsEnum<PdfCryptFilterMethod>(),
                entry.GetInteger(PdfTokens.LengthKey),
                entry.GetNameOrDefault(PdfTokens.AuthEventKey).AsEnum<PdfAuthEvent>());

            cryptFilters[name] = cryptFilter;
        }

        return cryptFilters;
    }
}
