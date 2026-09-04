using System.Runtime.ExceptionServices;
using Carried.Idempotency.Exceptions;
using Carried.Idempotency.Options;
using Carried.Idempotency.Serialization;
using Carried.Idempotency.Store;

namespace Carried.Idempotency;

public sealed class IdempotencyService
{
    private readonly IIdempotencyStore _store;
    private readonly IIdempotencySerializer _serializer;
    private readonly TimeProvider _timeProvider;

    private readonly TimeSpan _heartbeatInterval;

    public IdempotencyService(IIdempotencyStore store, IIdempotencySerializer serializer, IdempotencyOptions idempotencyOptions, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(idempotencyOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        idempotencyOptions.Validate();

        _store = store;
        _serializer = serializer;
        _timeProvider = timeProvider;
        _heartbeatInterval = TimeSpan.FromTicks(idempotencyOptions.LeaseDuration.Ticks / 3);
    }


    public static IdempotencyService CreateInMemory(IdempotencyOptions? options = null, TimeProvider? timeProvider = null)
    {
        options ??= new IdempotencyOptions();
        timeProvider ??= TimeProvider.System;
        
        var serializer = new JsonIdempotencySerializer();
        var store = new InMemoryIdempotencyStore(options, timeProvider);

        return new IdempotencyService(store, serializer, options, timeProvider);
    }

    public async Task<T?> ExecuteAsync<T>(
        IdempotencyKey key,
        string fingerprint,
        Func<CancellationToken, Task<T?>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(operation);

        IdempotencyAcquireResult acquireResult = await _store.TryAcquireAsync(key, fingerprint, cancellationToken);
        switch (acquireResult.Status)
        {
            case IdempotencyAcquireStatus.Acquired:
            {
                using var heartbeatStopCts = new CancellationTokenSource();
                using var leaseLossCts = new CancellationTokenSource();
                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, leaseLossCts.Token);

                Guid ownerToken = acquireResult.OwnerToken
                                  ?? throw new InvalidOperationException("Acquired result has no owner token.");

                T? operationResult = default;

                Task heartbeatTask = RunLeaseHeartbeatAsync(key, ownerToken, leaseLossCts, heartbeatStopCts.Token);

                ExceptionDispatchInfo? operationException = null;

                try
                {
                    operationResult = await operation(operationCts.Token);
                }
                catch (Exception ex)
                {
                    operationException = ExceptionDispatchInfo.Capture(ex);
                }

                if (operationException is not null)
                {
                    ExceptionDispatchInfo? heartbeatException =
                        await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);

                    await TryReleaseBestEffortAsync(key, ownerToken);

                    if (operationException.SourceException is OperationCanceledException &&
                        leaseLossCts.IsCancellationRequested &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        heartbeatException?.Throw();
                        throw new IdempotencyLeaseLostException();
                    }

                    operationException.Throw();
                }

                bool completed;

                try
                {
                    byte[] payload = _serializer.Serialize(operationResult);
                    completed = await _store.TryCompleteAsync(key, ownerToken, payload, CancellationToken.None);
                }
                catch
                {
                    await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);
                    throw;
                }

                ExceptionDispatchInfo? finalHeartbeatException = await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);

                if (completed) return operationResult;

                finalHeartbeatException?.Throw();

                throw new IdempotencyLeaseLostException();
            }
            case IdempotencyAcquireStatus.Completed:
            {
                byte[] payload = acquireResult.Payload
                                 ?? throw new InvalidOperationException("Completed result has no payload.");
                return _serializer.Deserialize<T>(payload);
            }
            case IdempotencyAcquireStatus.Conflict:
                throw new IdempotencyConflictException();
            case IdempotencyAcquireStatus.InProgress:
                throw new IdempotencyInProgressException();
            default:
                throw new InvalidOperationException($"Unknown acquire status: {acquireResult.Status}");
        }
    }

    private static async Task<ExceptionDispatchInfo?> StopHeartbeatAsync(CancellationTokenSource heartbeatStopCts, Task heartbeatTask)
    {
        await heartbeatStopCts.CancelAsync();

        try
        {
            await heartbeatTask;
            return null;
        }
        catch (Exception ex)
        {
            return ExceptionDispatchInfo.Capture(ex);
        }
    }

    private async ValueTask TryReleaseBestEffortAsync(
        IdempotencyKey key,
        Guid ownerToken)
    {
        try
        {
            await _store.TryReleaseAsync(
                key,
                ownerToken,
                CancellationToken.None);
        }
        catch
        {
            // TODO: observability
        }
    }

    private async Task RunLeaseHeartbeatAsync(IdempotencyKey key, Guid ownerToken, CancellationTokenSource leaseLostCts, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_heartbeatInterval, _timeProvider, cancellationToken);

                bool renewed = await _store.TryRenewLeaseAsync(key, ownerToken, cancellationToken);

                if (renewed) continue;
                await leaseLostCts.CancelAsync();
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected heartbeat shutdown
        }
        catch
        {
            await leaseLostCts.CancelAsync();
            throw;
        }
    }
}