using System.Text;
using AbrIT.Panel.Domain.Payments;
using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Domain.Tests.Provisioning;

public class SecurityHelpersTests
{
    private const string Secret = "unit-test-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"orderId\":\"abc123\"}");

    [Fact]
    public void Hmac_valid_signature_is_accepted_in_any_hex_case()
    {
        var signature = MockPaymentSignature.Compute(Secret, Body);

        Assert.Equal(64, signature.Length);
        Assert.True(MockPaymentSignature.Verify(Secret, Body, signature));
        Assert.True(MockPaymentSignature.Verify(Secret, Body, signature.ToUpperInvariant()));
    }

    [Fact]
    public void Hmac_invalid_signature_is_rejected()
    {
        var signature = MockPaymentSignature.Compute(Secret, Body);
        var tamperedBody = Encoding.UTF8.GetBytes("{\"orderId\":\"other\"}");

        Assert.False(MockPaymentSignature.Verify(Secret, tamperedBody, signature));
        Assert.False(MockPaymentSignature.Verify("wrong-secret", Body, signature));
        Assert.False(MockPaymentSignature.Verify(Secret, Body, "not-hex"));
        Assert.False(MockPaymentSignature.Verify(Secret, Body, signature[..10]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Hmac_missing_signature_is_rejected(string? signature) =>
        Assert.False(MockPaymentSignature.Verify(Secret, Body, signature));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hmac_empty_secret_never_verifies(string? secret) =>
        Assert.False(MockPaymentSignature.Verify(secret, Body, MockPaymentSignature.Compute(Secret, Body)));

    [Theory]
    [InlineData("POST https://admin:s3cr3t@cloud.example/ocs failed", "s3cr3t")]
    [InlineData("Authorization: Basic YWRtaW46czNjcjN0 rejected", "YWRtaW46czNjcjN0")]
    [InlineData("bad request password=hunter2&userid=x", "hunter2")]
    [InlineData("AppPassword: abc-def-ghi was refused", "abc-def-ghi")]
    public void Sanitizer_removes_secrets(string raw, string secret)
    {
        var safe = ErrorSanitizer.Sanitize(raw);
        Assert.DoesNotContain(secret, safe, StringComparison.Ordinal);
        Assert.Contains("***", safe, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_truncates_and_strips_control_characters()
    {
        var safe = ErrorSanitizer.Sanitize("line1\r\nline2\t" + new string('x', 1000));
        Assert.DoesNotContain('\n', safe);
        Assert.True(safe.Length <= ErrorSanitizer.MaxLength + 1);
        Assert.Equal("Unknown error.", ErrorSanitizer.Sanitize(null));
    }
}
