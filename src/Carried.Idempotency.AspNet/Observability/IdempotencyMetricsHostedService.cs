using Microsoft.Extensions.Hosting;

namespace Carried.Idempotency.AspNet.Observability;

internal sealed class IdempotencyMetricsHostedService : IHostedService
{
    private readonly IdempotencyMetrics _metrics;

    public IdempotencyMetricsHostedService(IdempotencyMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        _metrics = metrics;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _metrics.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _metrics.Stop();
        return Task.CompletedTask;
    }
}