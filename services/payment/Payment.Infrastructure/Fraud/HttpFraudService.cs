using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payment.Application.Fraud;

namespace Payment.Infrastructure.Fraud;

public sealed class HttpFraudService : IFraudService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpFraudService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public HttpFraudService(HttpClient httpClient, ILogger<HttpFraudService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FraudServiceResult> EvaluateAsync(
        Guid paymentId, decimal amount, string currency, string customerEmail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/fraud/evaluate",
                new { paymentId, amount, currency, customerEmail },
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FraudEvaluateResponse>(JsonOptions, cancellationToken);
            if (result is null)
                return Approve();

            return new FraudServiceResult(result.Decision, result.Score, result.RulesTriggered);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Degrade gracefully — fraud service unavailable should not block payments
            _logger.LogWarning(ex, "Fraud service unavailable for payment {PaymentId} — approving", paymentId);
            return Approve();
        }
    }

    private static FraudServiceResult Approve() =>
        new(FraudDecision.Approve, 0, []);

    private sealed record FraudEvaluateResponse(
        Guid DecisionId, Guid PaymentId, int Score, string Decision,
        List<string> RulesTriggered, DateTimeOffset EvaluatedAtUtc);
}
