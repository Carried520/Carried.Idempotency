using System.Diagnostics.Metrics;
using Carried.Idempotency.IdempotencyEvents;

namespace Carried.Idempotency.AspNet.Observability;

internal class IdempotencyMetrics : IDisposable , IIdempotencyMetricsRecorder
{
    internal const string MeterName = "Carried.Idempotency.AspNet";
    internal const string OperationsInstrumentName = "carried.idempotency.operations";
    internal const string ResponsesInstrumentName =
        "carried.idempotency.responses";
    

    private readonly IdempotencyService _idempotencyService;
    private readonly Meter _meter;
    private readonly Counter<long> _operations;
    private readonly Counter<long> _responses;

    public IdempotencyMetrics(IdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;

        _meter = new Meter(MeterName);

        _operations = _meter.CreateCounter<long>(
            OperationsInstrumentName,
            unit: "{operation}",
            description: "Number of idempotency lifecycle events.");
        
        _responses = _meter.CreateCounter<long>(
            ResponsesInstrumentName,
            unit: "{response}",
            description: "Number of idempotent HTTP response retention decisions.");
    }

    internal Task StartAsync(CancellationToken cancellationToken)
    {
        _idempotencyService.Acquired += OnAcquired;
        _idempotencyService.InProgress += OnInProgress;
        _idempotencyService.Replayed += OnReplayed;
        _idempotencyService.Conflict += OnConflict;
        _idempotencyService.Completed += OnCompleted;
        _idempotencyService.Released += OnReleased;
        _idempotencyService.ReleaseFailed += OnReleaseFailed;
        _idempotencyService.LeaseLost += OnLeaseLost;

        return Task.CompletedTask;
    }

    internal Task StopAsync(CancellationToken cancellationToken)
    {
        _idempotencyService.Acquired -= OnAcquired;
        _idempotencyService.InProgress -= OnInProgress;
        _idempotencyService.Replayed -= OnReplayed;
        _idempotencyService.Conflict -= OnConflict;
        _idempotencyService.Completed -= OnCompleted;
        _idempotencyService.Released -= OnReleased;
        _idempotencyService.ReleaseFailed -= OnReleaseFailed;
        _idempotencyService.LeaseLost -= OnLeaseLost;

        return Task.CompletedTask;
    }

    private void OnAcquired(
        object? sender,
        IdempotencyAcquiredEvent @event)
    {
        Record(Outcomes.Acquired);
    }

    private void OnInProgress(
        object? sender,
        IdempotencyInProgressEvent @event)
    {
        Record(Outcomes.InProgress);
    }

    private void OnReplayed(
        object? sender,
        IdempotencyReplayedEvent @event)
    {
        Record(Outcomes.Replayed);
    }

    private void OnConflict(
        object? sender,
        IdempotencyConflictEvent @event)
    {
        Record(Outcomes.Conflict);
    }

    private void OnCompleted(
        object? sender,
        IdempotencyCompletedEvent @event)
    {
        Record(Outcomes.Completed);
    }

    private void OnReleased(
        object? sender,
        IdempotencyReleasedEvent @event)
    {
        Record(Outcomes.Released);
    }

    private void OnReleaseFailed(
        object? sender,
        IdempotencyReleaseFailedEvent @event)
    {
        Record(Outcomes.ReleaseFailed);
    }

    private void OnLeaseLost(
        object? sender,
        IdempotencyLeaseLostEvent @event)
    {
        Record(Outcomes.LeaseLost);
    }

    private void Record(string outcome)
    {
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(
                "outcome",
                outcome));
    }
    
    public void RecordResponse(string result)
    {
        _responses.Add(
            1,
            new KeyValuePair<string, object?>(
                "result",
                result));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static class Outcomes
    {
        public const string Acquired = "acquired";
        public const string InProgress = "in_progress";
        public const string Replayed = "replayed";
        public const string Conflict = "conflict";
        public const string Completed = "completed";
        public const string Released = "released";
        public const string ReleaseFailed = "release_failed";
        public const string LeaseLost = "lease_lost";
    }
}