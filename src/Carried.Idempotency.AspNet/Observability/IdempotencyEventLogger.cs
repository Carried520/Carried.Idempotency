using Carried.Idempotency.IdempotencyEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carried.Idempotency.AspNet.Observability;

internal sealed partial class IdempotencyEventLogger : IHostedService
{
    private readonly IdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyEventLogger> _logger;

    public IdempotencyEventLogger(
        IdempotencyService idempotencyService,
        ILogger<IdempotencyEventLogger> logger)
    {
        _idempotencyService = idempotencyService;
        _logger = logger;
    }


    public Task StartAsync(CancellationToken cancellationToken)
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

    public Task StopAsync(CancellationToken cancellationToken)
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
        LogAcquired(@event.Key.Scope);
    }

    private void OnInProgress(
        object? sender,
        IdempotencyInProgressEvent @event)
    {
        LogInProgress(@event.Key.Scope);
    }

    private void OnReplayed(
        object? sender,
        IdempotencyReplayedEvent @event)
    {
        LogReplayed(@event.Key.Scope);
    }

    private void OnConflict(
        object? sender,
        IdempotencyConflictEvent @event)
    {
        LogConflict(@event.Key.Scope);
    }

    private void OnCompleted(
        object? sender,
        IdempotencyCompletedEvent @event)
    {
        LogCompleted(@event.Key.Scope);
    }

    private void OnReleased(
        object? sender,
        IdempotencyReleasedEvent @event)
    {
        LogReleased(@event.Key.Scope);
    }

    private void OnReleaseFailed(
        object? sender,
        IdempotencyReleaseFailedEvent @event)
    {
        LogReleaseFailed(@event.Exception, @event.Key.Scope);
    }

    private void OnLeaseLost(
        object? sender,
        IdempotencyLeaseLostEvent @event)
    {
        LogLeaseLost(@event.Key.Scope);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Idempotency execution acquired for scope {Scope}.")]
    private partial void LogAcquired(string scope);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Idempotency execution is already in progress for scope {Scope}.")]
    private partial void LogInProgress(string scope);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Idempotency result replayed for scope {Scope}.")]
    private partial void LogReplayed(string scope);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Idempotency conflict detected for scope {Scope}.")]
    private partial void LogConflict(string scope);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Idempotency execution completed for scope {Scope}.")]
    private partial void LogCompleted(string scope);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "Idempotency execution released for scope {Scope}.")]
    private partial void LogReleased(string scope);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "Failed to release idempotency execution for scope {Scope}.")]
    private partial void LogReleaseFailed(
        Exception exception,
        string scope);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Idempotency execution lost its lease for scope {Scope}.")]
    private partial void LogLeaseLost(string scope);
}