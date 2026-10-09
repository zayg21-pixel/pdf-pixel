using PdfPixel.Encryption.Cryptography;
using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Encryption;

/// <summary>
/// Base decryptor that exposes unified byte decryption for both streams and string objects.
/// Implementations derive the file key.
/// </summary>
public abstract class BasePdfDecryptor
{
    private const int AesBlockSize = 16;
    private const int MaxObjectKeyLength = 16;

    private static readonly byte[] AesSalt = [0x73, 0x41, 0x6C, 0x54];
    private static readonly PdfPasswordCredential EmptyPassword = new(string.Empty);

    private readonly PdfCredentialRequestedCallback? _onCredentialRequested;
    private readonly object _authenticationLock = new();
    private volatile byte[]? _fileKey;

    /// <summary>
    /// Initializes the decryptor with the encryption parameters parsed from the PDF /Encrypt dictionary.
    /// </summary>
    /// <param name="parameters">Encryption parameters of the document.</param>
    /// <param name="onCredentialRequested">Called when the empty user password does not authenticate, or null to try only the empty password.</param>
    protected BasePdfDecryptor(PdfDecryptorParameters parameters, PdfCredentialRequestedCallback? onCredentialRequested)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _onCredentialRequested = onCredentialRequested;
    }

    /// <summary>
    /// Encryption parameters parsed from the document's /Encrypt dictionary.
    /// </summary>
    public PdfDecryptorParameters Parameters { get; }

    /// <summary>
    /// Decrypt raw bytes belonging to an indirect string object identified by its reference.
    /// </summary>
    /// <param name="data">Encrypted (or plain) bytes.</param>
    /// <param name="reference">Owning object reference.</param>
    /// <exception cref="PdfAuthenticationException">Thrown if the string filter requires a credential and none of the supplied credentials is accepted.</exception>
    public byte[] DecryptString(in ReadOnlyMemory<byte> data, in PdfReference reference)
    {
        PdfCryptFilter cryptFilter = Parameters.StringCryptFilter;
        if (data.IsEmpty || cryptFilter.Method == PdfCryptFilterMethod.None)
        {
            return data.ToArray();
        }

        byte[] fileKey = Authenticate(cryptFilter.AuthEvent);

        ReadOnlySpan<byte> encrypted = data.Span;
        int ivLength = GetIvLength(cryptFilter);
        if (encrypted.Length < ivLength)
        {
            return Array.Empty<byte>();
        }

        IDecryptionCipher cipher = CreateCipher(encrypted.Slice(0, ivLength), reference, cryptFilter, fileKey);
        ReadOnlySpan<byte> body = encrypted.Slice(ivLength);
        int wholeBlocksLength = body.Length - (body.Length % cipher.BlockSize);

        var decrypted = new byte[wholeBlocksLength];
        cipher.Decrypt(body.Slice(0, wholeBlocksLength), decrypted);

        if (wholeBlocksLength < cipher.BlockSize)
        {
            return decrypted;
        }

        int paddingLength = cipher.GetPaddingLength(decrypted.AsSpan(wholeBlocksLength - cipher.BlockSize));
        if (paddingLength == 0)
        {
            return decrypted;
        }

        return decrypted.AsSpan(0, wholeBlocksLength - paddingLength).ToArray();
    }

    /// <summary>
    /// Wraps the specified stream in a forward-only stream that decrypts its contents.
    /// </summary>
    /// <param name="stream">The input stream containing the encrypted data. The returned stream takes ownership of it.</param>
    /// <param name="reference">The PDF reference used to determine the decryption parameters.</param>
    /// <param name="cryptFilter">Crypt filter selected for the stream.</param>
    /// <returns>A stream containing the decrypted data. The caller is responsible for disposing of the returned stream.</returns>
    /// <exception cref="PdfAuthenticationException">Thrown if the crypt filter requires a credential and none of the supplied credentials is accepted.</exception>
    public Stream DecryptStream(Stream stream, in PdfReference reference, PdfCryptFilter cryptFilter)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (cryptFilter == null)
        {
            throw new ArgumentNullException(nameof(cryptFilter));
        }

        if (cryptFilter.Method == PdfCryptFilterMethod.None)
        {
            return stream;
        }

        byte[] fileKey = Authenticate(cryptFilter.AuthEvent);

        var iv = new byte[GetIvLength(cryptFilter)];
        int ivRead = 0;
        while (ivRead < iv.Length)
        {
            int read = stream.Read(iv, ivRead, iv.Length - ivRead);
            if (read <= 0)
            {
                stream.Dispose();
                return Stream.Null;
            }

            ivRead += read;
        }

        return new PdfDecryptStream(stream, CreateCipher(iv, reference, cryptFilter, fileKey));
    }

    /// <summary>
    /// Selects the crypt filter for a stream from its dictionary and filter chain.
    /// </summary>
    internal PdfCryptFilter GetStreamCryptFilter(PdfDictionary streamDictionary, List<PdfFilterType> filters)
    {
        if (streamDictionary == null)
        {
            throw new ArgumentNullException(nameof(streamDictionary));
        }

        if (filters == null)
        {
            throw new ArgumentNullException(nameof(filters));
        }

        if (filters.Count > 0 && filters[0] == PdfFilterType.Crypt)
        {
            PdfDictionary? cryptParameters = streamDictionary.GetArray(PdfTokens.DecodeParmsKey)?.GetDictionary(0)
                ?? streamDictionary.GetDictionary(PdfTokens.DecodeParmsKey);

            return Parameters.GetCryptFilter(cryptParameters?.GetName(PdfTokens.NameKey));
        }

        PdfString? type = streamDictionary.GetName(PdfTokens.TypeKey);
        if (type == PdfTokens.EmbeddedFileKey)
        {
            return Parameters.EmbeddedFileCryptFilter;
        }

        if (type == PdfTokens.MetadataKey && !Parameters.EncryptMetadata)
        {
            return PdfCryptFilter.Identity;
        }

        return Parameters.StreamCryptFilter;
    }

    /// <summary>
    /// Authenticates the document if any of its stream, string or embedded file filters requires
    /// authentication when the document is opened.
    /// </summary>
    /// <exception cref="PdfAuthenticationException">Thrown if none of the supplied credentials is accepted.</exception>
    internal void AuthenticateOnOpen()
    {
        bool requiresAuthentication = RequiresAuthenticationOnOpen(Parameters.StreamCryptFilter)
            || RequiresAuthenticationOnOpen(Parameters.StringCryptFilter)
            || RequiresAuthenticationOnOpen(Parameters.EmbeddedFileCryptFilter);

        if (requiresAuthentication)
        {
            Authenticate(PdfAuthEvent.DocumentOpen);
        }
    }

    /// <summary>
    /// Validates <paramref name="credential"/> and computes the file key.
    /// </summary>
    /// <returns>The file key, or null when the credential is not accepted.</returns>
    protected abstract byte[]? TryComputeFileKey(PdfCredential credential);

    private byte[] Authenticate(PdfAuthEvent authEvent)
    {
        byte[]? fileKey = _fileKey;
        if (fileKey != null)
        {
            return fileKey;
        }

        lock (_authenticationLock)
        {
            fileKey = _fileKey;
            if (fileKey != null)
            {
                return fileKey;
            }

            fileKey = TryComputeFileKey(EmptyPassword);
            if (fileKey != null)
            {
                _fileKey = fileKey;
                return fileKey;
            }

            if (_onCredentialRequested == null)
            {
                throw new PdfAuthenticationException();
            }

            var reason = PdfCredentialRequestReason.CredentialRequired;
            while (true)
            {
                PdfCredentialRequest request = new(reason, authEvent);
                PdfCredential? credential = _onCredentialRequested(request);
                if (credential == null)
                {
                    throw new PdfAuthenticationException();
                }

                fileKey = TryComputeFileKey(credential);
                if (fileKey != null)
                {
                    _fileKey = fileKey;
                    return fileKey;
                }

                reason = PdfCredentialRequestReason.CredentialRejected;
            }
        }
    }

    private static bool RequiresAuthenticationOnOpen(PdfCryptFilter cryptFilter)
    {
        return cryptFilter.Method != PdfCryptFilterMethod.None
            && cryptFilter.AuthEvent == PdfAuthEvent.DocumentOpen;
    }

    /// <summary>
    /// Gets the length of the initialization vector that starts data encrypted with <paramref name="cryptFilter"/>.
    /// </summary>
    private static int GetIvLength(PdfCryptFilter cryptFilter)
    {
        if (cryptFilter.Method == PdfCryptFilterMethod.AESV2 || cryptFilter.Method == PdfCryptFilterMethod.AESV3)
        {
            return AesBlockSize;
        }

        return 0;
    }

    private static IDecryptionCipher CreateCipher(in ReadOnlySpan<byte> iv, in PdfReference reference, PdfCryptFilter cryptFilter, byte[] fileKey)
    {
        return cryptFilter.Method switch
        {
            PdfCryptFilterMethod.AESV2 => new AesCbc(DeriveObjectKey(fileKey, reference, AesSalt), iv),
            PdfCryptFilterMethod.AESV3 => new AesCbc(fileKey, iv),
            _ => new Rc4(DeriveObjectKey(fileKey, reference, ReadOnlySpan<byte>.Empty))
        };
    }

    /// <summary>
    /// Algorithm 1 (ISO 32000-2, 7.6.3.3): MD5 of the file key, object number, generation and optional AES salt.
    /// </summary>
    private static byte[] DeriveObjectKey(byte[] fileKey, in PdfReference reference, in ReadOnlySpan<byte> salt)
    {
        Span<byte> buffer = stackalloc byte[fileKey.Length + 5 + salt.Length];
        fileKey.CopyTo(buffer);
        uint objectNumber = reference.ObjectNumber;
        int generation = reference.Generation;
        buffer[fileKey.Length + 0] = (byte)(objectNumber & 0xFF);
        buffer[fileKey.Length + 1] = (byte)((objectNumber >> 8) & 0xFF);
        buffer[fileKey.Length + 2] = (byte)((objectNumber >> 16) & 0xFF);
        buffer[fileKey.Length + 3] = (byte)(generation & 0xFF);
        buffer[fileKey.Length + 4] = (byte)((generation >> 8) & 0xFF);
        salt.CopyTo(buffer.Slice(fileKey.Length + 5));

        int keyLength = Math.Min(fileKey.Length + 5, MaxObjectKeyLength);
        return Md5.ComputeHash(buffer).AsSpan(0, keyLength).ToArray();
    }
}
