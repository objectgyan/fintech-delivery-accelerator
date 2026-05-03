namespace Payment.Application.Providers;

public sealed record ProviderResult(
    bool Success,
    string? ProviderReference,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTransient
);
