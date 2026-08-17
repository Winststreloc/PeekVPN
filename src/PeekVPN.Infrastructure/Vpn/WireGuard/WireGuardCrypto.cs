using System.Buffers.Binary;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// WireGuard primitives from the published protocol: X25519, BLAKE2s, HMAC-BLAKE2s, ChaCha20-Poly1305.
/// </summary>
internal static class WireGuardCrypto
{
    public static readonly byte[] Construction = "Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s"u8.ToArray();
    public static readonly byte[] Identifier = "WireGuard v1 zx2c4 Jason@zx2c4.com"u8.ToArray();
    public static readonly byte[] LabelMac1 = "mac1----"u8.ToArray();
    public static readonly byte[] LabelCookie = "cookie--"u8.ToArray();

    public static byte[] Hash(ReadOnlySpan<byte> input)
    {
        var digest = new Blake2sDigest(256);
        if (input.Length > 0)
        {
            var copy = input.ToArray();
            digest.BlockUpdate(copy, 0, copy.Length);
        }

        var output = new byte[32];
        digest.DoFinal(output, 0);
        return output;
    }

    public static byte[] ConcatHash(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var digest = new Blake2sDigest(256);
        digest.BlockUpdate(a.ToArray(), 0, a.Length);
        digest.BlockUpdate(b.ToArray(), 0, b.Length);
        var output = new byte[32];
        digest.DoFinal(output, 0);
        return output;
    }

    public static byte[] Hmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        var hmac = new HMac(new Blake2sDigest(256));
        hmac.Init(new KeyParameter(key.ToArray()));
        if (data.Length > 0)
        {
            var copy = data.ToArray();
            hmac.BlockUpdate(copy, 0, copy.Length);
        }

        var output = new byte[32];
        hmac.DoFinal(output, 0);
        return output;
    }

    public static byte[] Mac16(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        var digest = new Blake2sDigest(key.ToArray(), 16, null, null);
        if (data.Length > 0)
        {
            var copy = data.ToArray();
            digest.BlockUpdate(copy, 0, copy.Length);
        }

        var output = new byte[16];
        digest.DoFinal(output, 0);
        return output;
    }

    public static byte[] GeneratePrivateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        key[0] &= 248;
        key[31] &= 127;
        key[31] |= 64;
        return key;
    }

    public static byte[] PublicFromPrivate(ReadOnlySpan<byte> privateKey)
    {
        var priv = new X25519PrivateKeyParameters(privateKey.ToArray(), 0);
        return priv.GeneratePublicKey().GetEncoded();
    }

    public static byte[] Dh(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> publicKey)
    {
        var agreement = new X25519Agreement();
        agreement.Init(new X25519PrivateKeyParameters(privateKey.ToArray(), 0));
        var shared = new byte[32];
        agreement.CalculateAgreement(new X25519PublicKeyParameters(publicKey.ToArray(), 0), shared, 0);
        return shared;
    }

    public static byte[] AeadEncrypt(
        ReadOnlySpan<byte> key,
        ulong counter,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> aad)
    {
        Span<byte> nonce = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(nonce[4..], counter);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aead = new ChaCha20Poly1305(key.ToArray());
        aead.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        var combined = new byte[ciphertext.Length + tag.Length];
        ciphertext.CopyTo(combined, 0);
        tag.CopyTo(combined, ciphertext.Length);
        return combined;
    }

    public static bool TryAeadDecrypt(
        ReadOnlySpan<byte> key,
        ulong counter,
        ReadOnlySpan<byte> ciphertextAndTag,
        ReadOnlySpan<byte> aad,
        out byte[] plaintext)
    {
        plaintext = [];
        if (ciphertextAndTag.Length < 16)
        {
            return false;
        }

        Span<byte> nonce = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(nonce[4..], counter);

        var cipherLen = ciphertextAndTag.Length - 16;
        var ciphertext = ciphertextAndTag[..cipherLen];
        var tag = ciphertextAndTag[cipherLen..];
        var output = new byte[cipherLen];

        try
        {
            using var aead = new ChaCha20Poly1305(key.ToArray());
            aead.Decrypt(nonce, ciphertext, tag, output, aad);
            plaintext = output;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static byte[] Tai64nNow()
    {
        var now = DateTimeOffset.UtcNow;
        var secs = (ulong)now.ToUnixTimeSeconds() + 0x400000000000000aUL;
        var nanos = (uint)((now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond) * 100) & ~0x00FFFFFF;
        var stamp = new byte[12];
        stamp[0] = (byte)(secs >> 56);
        stamp[1] = (byte)(secs >> 48);
        stamp[2] = (byte)(secs >> 40);
        stamp[3] = (byte)(secs >> 32);
        stamp[4] = (byte)(secs >> 24);
        stamp[5] = (byte)(secs >> 16);
        stamp[6] = (byte)(secs >> 8);
        stamp[7] = (byte)secs;
        stamp[8] = (byte)(nanos >> 24);
        stamp[9] = (byte)(nanos >> 16);
        stamp[10] = (byte)(nanos >> 8);
        stamp[11] = (byte)nanos;
        return stamp;
    }
}
