using System.Runtime.ExceptionServices;
using Carried.Idempotency.Exceptions;
using Carried.Idempotency.IdempotencyEvents;
using Carried.Idempotency.IdempotencyOperation;
using Carried.Idempotency.Options;
using Carried.Idempotency.Serialization;
using Carried.Idempotency.Store;

namespace Carried.Idempotency;

/// <summary>
/// Coordinates execution of idempotent operations, including key acquisition,
/// result replay, lease renewal, and completion.
/// </summary>
public sealed class IdempotencyService
{
    private readonly IIdempotencyStore _store;
    private readonly IIdempotencySerializer _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _heartbeatInterval;

    /// <summary>
    /// Occurs when ownership of an idempotency key is successfully acquired.
    /// </summary>
    public event EventHandler<IdempotencyAcquiredEvent>? Acquired;

    /// <summary>
    /// Occurs when an operation encounters an idempotency key that is already in progress.
    /// </summary>
    public event EventHandler<IdempotencyInProgressEvent>? InProgress;

    /// <summary>
    /// Occurs when a previously completed operation is replayed.
    /// </summary>
    public event EventHandler<IdempotencyReplayedEvent>? Replayed;

    /// <summary>
    /// Occurs when an idempotency key is reused for a different operation.
    /// </summary>
    public event EventHandler<IdempotencyConflictEvent>? Conflict;

    /// <summary>
    /// Occurs when an idempotent operation is successfully completed and retained for replay.
    /// </summary>
    public event EventHandler<IdempotencyCompletedEvent>? Completed;

    /// <summary>
    /// Occurs when ownership of an idempotency key is successfully released.
    /// </summary>
    public event EventHandler<IdempotencyReleasedEvent>? Released;

    /// <summary>
    /// Occurs when a best-effort release of an idempotency key fails.
    /// </summary>
    public event EventHandler<IdempotencyReleaseFailedEvent>? ReleaseFailed;

    /// <summary>
    /// Occurs when ownership of an idempotency key is lost before the requested state transition can be applied.
    /// </summary>
    public event EventHandler<IdempotencyLeaseLostEvent>? LeaseLost;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyService"/> class.
    /// </summary>
    /// <param name="store">
    /// The idempotency store used to coordinate operation state.
    /// </param>
    /// <param name="serializer">
    /// The serializer used to retain and replay completed operation results.
    /// </param>
    /// <param name="idempotencyOptions">
    /// The options that configure idempotency behavior.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider used for lease renewal timing.
    /// </param>
    public IdempotencyService(IIdempotencyStore store,
        IIdempotencySerializer serializer,
        IdempotencyOptions idempotencyOptions,
        TimeProvider timeProvider)
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

    /// <summary>
    /// Creates an idempotency service backed by an in-memory store and JSON serialization.
    /// </summary>
    /// <param name="options">
    /// The options that configure idempotency behavior, or <see langword="null"/>
    /// to use the defaults.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider to use, or <see langword="null"/> to use
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <returns>
    /// A new in-memory idempotency service.
    /// </returns>
    public static IdempotencyService CreateInMemory(IdempotencyOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        options ??= new IdempotencyOptions();
        timeProvider ??= TimeProvider.System;

        var serializer = new JsonIdempotencySerializer();
        var store = new InMemoryIdempotencyStore(options, timeProvider);

        return new IdempotencyService(store, serializer, options, timeProvider);
    }


    /// <summary>
    /// Creates an idempotency service using the specified store and serializer.
    /// </summary>
    /// <param name="store">
    /// The idempotency store used to coordinate operation state.
    /// </param>
    /// <param name="serializer">
    /// The serializer used to retain and replay completed operation results.
    /// </param>
    /// <param name="options">
    /// The options that configure idempotency behavior.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider to use, or <see langword="null"/> to use
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <returns>
    /// A new idempotency service.
    /// </returns>
    public static IdempotencyService Create(
        IIdempotencyStore store,
        IIdempotencySerializer serializer,
        IdempotencyOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(options);

        return new IdempotencyService(
            store,
            serializer,
            options,
            timeProvider ?? TimeProvider.System);
    }

    /// <summary>
    /// Executes an operation under the supplied idempotency key.
    /// </summary>
    ///
    /// <remarks>
    /// If the key is acquired, the operation is executed while the service periodically
    /// renews its ownership lease. The operation determines whether its result is retained
    /// for replay or ownership is released without retaining the result.
    ///
    /// If a completed entry already exists with the same fingerprint, its stored result
    /// is returned without executing the operation again.
    ///
    /// Reusing an active retained key with a different fingerprint results in an
    /// <see cref="IdempotencyConflictException"/>. Attempting to execute while another
    /// caller currently owns the key results in an
    /// <see cref="IdempotencyInProgressException"/>.
    ///
    /// If ownership of the key is lost before the requested state transition can be applied,
    /// an <see cref="IdempotencyLeaseLostException"/> is thrown.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of value produced by the operation.
    /// </typeparam>
    /// <param name="key">
    /// The idempotency key identifying the operation.
    /// </param>
    /// <param name="fingerprint">
    /// The fingerprint used to determine whether reuse of the key represents the same operation.
    /// </param>
    /// <param name="operation">
    /// The operation to execute after ownership of the key is acquired.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel idempotency processing.
    /// </param>
    /// <returns>
    /// The result produced by the executed operation or replayed from a previously completed operation.
    /// </returns>
    /// <exception cref="IdempotencyConflictException">
    /// Thrown when the idempotency key is already associated with a different operation.
    /// </exception>
    /// <exception cref="IdempotencyInProgressException">
    /// Thrown when another operation currently owns the idempotency key.
    /// </exception>
    /// <exception cref="IdempotencyLeaseLostException">
    /// Thrown when ownership of the idempotency key is lost before the requested state transition can be applied.
    /// </exception>
    public async Task<T?> ExecuteAsync<T>(
        IdempotencyKey key,
        string fingerprint,
        Func<CancellationToken, Task<IdempotencyOperationResult<T>>> operation,
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
                using var operationCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, leaseLossCts.Token);

                Guid ownerToken = acquireResult.OwnerToken
                                  ?? throw new InvalidOperationException("Acquired result has no owner token.");
                Emit(Acquired, new IdempotencyAcquiredEvent(key));

                IdempotencyOperationResult<T>? operationResult = null;

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
                        Emit(LeaseLost, new IdempotencyLeaseLostEvent(key));
                        throw new IdempotencyLeaseLostException();
                    }

                    operationException.Throw();
                }

                if (operationResult is null)
                {
                    await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);
                    await TryReleaseBestEffortAsync(key, ownerToken);
                    throw new InvalidOperationException("The idempotency operation returned a null result.");
                }

                bool transitionSucceeded;

                try
                {
                    switch (operationResult.Outcome)
                    {
                        case IdempotencyOperationOutcome.Complete:
                        {
                            byte[] payload = _serializer.Serialize(operationResult.Value);
                            transitionSucceeded = await _store.TryCompleteAsync(
                                key,
                                ownerToken,
                                payload,
                                CancellationToken.None);
                            break;
                        }
                        case IdempotencyOperationOutcome.Release:
                            transitionSucceeded = await _store.TryReleaseAsync(key, ownerToken, CancellationToken.None);
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Unknown operation outcome: {operationResult.Outcome}");
                    }
                }
                catch
                {
                    await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);
                    throw;
                }

                ExceptionDispatchInfo? finalHeartbeatException =
                    await StopHeartbeatAsync(heartbeatStopCts, heartbeatTask);

                if (transitionSucceeded)
                {
                    switch (operationResult.Outcome)
                    {
                        case IdempotencyOperationOutcome.Complete:
                            Emit(Completed, new IdempotencyCompletedEvent(key));
                            break;
                        case IdempotencyOperationOutcome.Release:
                            Emit(Released, new IdempotencyReleasedEvent(key));
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Unknown operation outcome: {operationResult.Outcome}");
                    }

                    return operationResult.Value;
                }

                finalHeartbeatException?.Throw();

                Emit(LeaseLost, new IdempotencyLeaseLostEvent(key));
                throw new IdempotencyLeaseLostException();
            }
            case IdempotencyAcquireStatus.Completed:
            {
                Emit(Replayed, new IdempotencyReplayedEvent(key));
                byte[] payload = acquireResult.Payload
                                 ?? throw new InvalidOperationException("Completed result has no payload.");
                return _serializer.Deserialize<T>(payload);
            }
            case IdempotencyAcquireStatus.Conflict:
                Emit(Conflict, new IdempotencyConflictEvent(key));
                throw new IdempotencyConflictException();
            case IdempotencyAcquireStatus.InProgress:
                Emit(InProgress, new IdempotencyInProgressEvent(key));
                throw new IdempotencyInProgressException();
            default:
                throw new InvalidOperationException($"Unknown acquire status: {acquireResult.Status}");
        }
    }

    private static async Task<ExceptionDispatchInfo?> StopHeartbeatAsync(CancellationTokenSource heartbeatStopCts,
        Task heartbeatTask)
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
            bool released = await _store.TryReleaseAsync(
                key,
                ownerToken,
                CancellationToken.None);

            if (released)
                Emit(Released, new IdempotencyReleasedEvent(key));
        }
        catch (Exception exception)
        {
            Emit(ReleaseFailed, new IdempotencyReleaseFailedEvent(key, exception));
        }
    }

    private async Task RunLeaseHeartbeatAsync(IdempotencyKey key,
        Guid ownerToken,
        CancellationTokenSource leaseLostCts,
        CancellationToken cancellationToken)
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

    private void Emit<TEvent>(EventHandler<TEvent>? handlers, TEvent @event)
    {
        if (handlers is null)
            return;

        foreach (Delegate subscriber in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<TEvent>)subscriber)(this, @event);
            }
            catch
            {
                // event subscribers cannot affect idempotency execution
            }
        }
    }
}