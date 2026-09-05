using Microsoft.Extensions.Hosting;

namespace Carried.Idempotency.AspNet.Observability;

internal sealed class IdempotencyMetricsHostedService :
    IHostedService
{
    private readonly IdempotencyMetrics _metrics;

    public IdempotencyMetricsHostedService(
        IdempotencyMetrics metrics)
    {
        _metrics = metrics;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _metrics.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _metrics.StopAsync(cancellationToken);
    }
}