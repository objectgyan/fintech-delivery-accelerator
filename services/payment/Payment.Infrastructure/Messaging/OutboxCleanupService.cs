using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Repositories;

namespace Payment.Infrastructure.Messaging;

internal sealed class OutboxCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly TimeSpan _retentionPeriod;
    private readonly TimeSpan _runInterval;

    public OutboxCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupService> logger,
        TimeSpan? retentionPeriod = null,
        TimeSpan? runInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
        _runInterval = runInterval ?? TimeSpan.FromHours(24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxCleanup started — retention {RetentionDays}d, runs every {IntervalHours}h",
            _retentionPeriod.TotalDays, _runInterval.TotalHours);

        // Run once on startup, then on the configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxCleanup: unhandled error during cleanup run");
            }

            try
            {
                await Task.Delay(_runInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxCleanup stopped");
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTimeOffset.UtcNow - _retentionPeriod;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        var deleted = await repository.DeletePublishedOutboxMessagesAsync(threshold, cancellationToken);

        if (deleted > 0)
            _logger.LogInformation(
                "OutboxCleanup: deleted {Count} published message(s) older than {ThresholdDays}d",
                deleted, _retentionPeriod.TotalDays);
        else
            _logger.LogDebug("OutboxCleanup: no published messages eligible for deletion");
    }
}
