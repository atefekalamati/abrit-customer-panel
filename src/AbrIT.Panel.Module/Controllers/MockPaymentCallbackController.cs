using System.Text.Json;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AbrIT.Panel.Module.Controllers;

/// <summary>
/// Simulated payment-gateway callback (DECISIONS A-09): <c>POST /api/payments/mock-callback</c> with body
/// <c>{"orderId":"..."}</c> and <c>X-Signature</c> = hex HMAC-SHA256(raw body, MockPayment__WebhookSecret).
/// Returns 404 while the secret is empty, so the endpoint does not exist unless explicitly configured.
/// </summary>
[IgnoreAntiforgeryToken]
public sealed class MockPaymentCallbackController(
    MockPaymentOptions options,
    IPaymentApprovalService approvals,
    ILogger<MockPaymentCallbackController> logger) : Controller
{
    private const int MaxBodyBytes = 4 * 1024;

    [HttpPost]
    public async Task<IActionResult> Post(CancellationToken ct)
    {
        if (!options.IsEnabled)
        {
            return NotFound();
        }

        if (Request.ContentLength > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = await ReadBodyAsync(ct);
        if (body is null)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (!MockPaymentSignature.Verify(options.WebhookSecret, body, Request.Headers[MockPaymentSignature.HeaderName]))
        {
            logger.LogWarning("Mock payment callback rejected: missing or invalid signature");
            return Unauthorized();
        }

        string? orderId;
        try
        {
            using var json = JsonDocument.Parse(body);
            orderId = json.RootElement.TryGetProperty("orderId", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(orderId))
        {
            return BadRequest();
        }

        try
        {
            var order = await approvals.ApproveFromVerifiedCallbackAsync(orderId, ct);
            logger.LogInformation("Mock payment callback for order {OrderId}: status {Status}", order.OrderId, order.Status);
            return Ok(new { orderId = order.OrderId, status = order.Status.ToString() });
        }
        catch (OrderNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOrderTransitionException ex)
        {
            return Conflict(new { orderId, status = ex.From.ToString() });
        }
    }

    private async Task<byte[]?> ReadBodyAsync(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
