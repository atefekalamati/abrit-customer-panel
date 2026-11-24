using System.Security.Cryptography;
using System.Text;

namespace AbrIT.Panel.Domain.Payments;

/// <summary>
/// Signature of the simulated payment callback (DECISIONS A-09):
/// <c>X-Signature = lowercase hex(HMAC-SHA256(raw request body, MockPayment__WebhookSecret))</c>.
/// </summary>
public static class MockPaymentSignature
{
    public const string HeaderName = "X-Signature";

    public static string Compute(string secret, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return Convert.ToHexStringLower(mac);
    }

    /// <summary>Constant-time comparison; accepts upper- or lower-case hex. Empty secret or signature → false.</summary>
    public static bool Verify(string? secret, ReadOnlySpan<byte> body, string? signatureHex)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(signatureHex))
        {
            return false;
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
