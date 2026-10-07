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

    private readonly PdfPasswordRequestedCallback? _onPasswordRequested;
    private readonly object _authenticationLock = new();
    private volatile byte[]? _fileKey;

    /// <summary>
    /// Initializes the decryptor with the encryption parameters parsed from the PDF /Encrypt dictionary.
    /// </summary>
    /// <param name="parameters">Encryption parameters of the document.</param>
    /// <param name="onPasswordRequested">Called when the empty user password does not authenticate, or null to try only the empty password.</param>
    protected BasePdfDecryptor(PdfDecryptorParameters parameters, PdfPasswordRequestedCallback? onPasswordRequested)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _onPasswordRequested = onPasswordRequested;
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
    /// <exception cref="PdfIncorrectPasswordException">Thrown if the string filter requires a password and none of the supplied passwords is correct.</exception>
    public byte[] DecryptString(in ReadOnlyMemory<byte> data, in PdfReference reference)
    {
        PdfCryptFilter cryptFilter = Parameters.StringCryptFilter;
        if (data.IsEmpty || cryptFilter.Method == PdfCryptFilterMethod.None)
        {
            return data.ToArray();
        }

        byte[] fileKey = Authenticate(cryptFilter.AuthEvent);
        return Decrypt(data.Span, reference, cryptFilter, fileKey);
    }

    /// <summary>
    /// Decrypts the contents of the specified stream using the provided PDF reference.
    /// </summary>
    /// <remarks>The method reads the entire content of the input stream, decrypts it, and returns a
    /// new memory stream containing the decrypted data. The input stream is not modified or disposed by this
    /// method.</remarks>
    /// <param name="stream">The input stream containing the encrypted data. The stream must be readable.</param>
    /// <param name="reference">The PDF reference used to determine the decryption parameters.</param>
    /// <param name="cryptFilter">Crypt filter selected for the stream.</param>
    /// <returns>A stream containing the decrypted data. The caller is responsible for disposing of the returned stream.</returns>
    /// <exception cref="PdfIncorrectPasswordException">Thrown if the crypt filter requires a password and none of the supplied passwords is correct.</exception>
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

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        byte[] decryptedBytes = (memoryStream.Length == 0)
            ? Array.Empty<byte>()
            : Decrypt(memoryStream.ToArray(), reference, cryptFilter, fileKey);

        return new MemoryStream(decryptedBytes);
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
    /// <exception cref="PdfIncorrectPasswordException">Thrown if none of the supplied passwords is correct.</exception>
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
    /// Validates <paramref name="password"/> as the user or owner password and computes the file key.
    /// </summary>
    /// <returns>The file key, or null when the password is not valid.</returns>
    protected abstract byte[]? TryComputeFileKey(string password);

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

            fileKey = TryComputeFileKey(string.Empty);
            if (fileKey != null)
            {
                _fileKey = fileKey;
                return fileKey;
            }

            if (_onPasswordRequested == null)
            {
                throw new PdfIncorrectPasswordException();
            }

            var reason = PdfPasswordRequestReason.PasswordRequired;
            while (true)
            {
                string? password = _onPasswordRequested(reason, authEvent);
                if (password == null)
                {
                    throw new PdfIncorrectPasswordException();
                }

                fileKey = TryComputeFileKey(password);
                if (fileKey != null)
                {
                    _fileKey = fileKey;
                    return fileKey;
                }

                reason = PdfPasswordRequestReason.IncorrectPassword;
            }
        }
    }

    private static bool RequiresAuthenticationOnOpen(PdfCryptFilter cryptFilter)
    {
        return cryptFilter.Method != PdfCryptFilterMethod.None
            && cryptFilter.AuthEvent == PdfAuthEvent.DocumentOpen;
    }

    private static byte[] Decrypt(in ReadOnlySpan<byte> data, in PdfReference reference, PdfCryptFilter cryptFilter, byte[] fileKey)
    {
        return cryptFilter.Method switch
        {
            PdfCryptFilterMethod.AESV2 => DecryptAes(DeriveObjectKey(fileKey, reference, AesSalt), data),
            PdfCryptFilterMethod.AESV3 => DecryptAes(fileKey, data),
            _ => Rc4.Transform(DeriveObjectKey(fileKey, reference, ReadOnlySpan<byte>.Empty), data)
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

    private static byte[] DecryptAes(byte[] key, in ReadOnlySpan<byte> data)
    {
        if (data.Length < AesBlockSize)
        {
            return data.ToArray();
        }

        return AesCbc.Decrypt(key, data.Slice(0, AesBlockSize), data.Slice(AesBlockSize), stripPkcs7Padding: true);
    }
}
