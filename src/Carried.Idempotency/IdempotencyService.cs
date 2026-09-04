using Carried.Idempotency.Exceptions;
using Carried.Idempotency.Serialization;

namespace Carried.Idempotency;

public sealed class IdempotencyService
{
    private readonly IIdempotencyStore _store;
    private readonly IIdempotencySerializer _serializer;

    public IdempotencyService(IIdempotencyStore store, IIdempotencySerializer serializer)
    {
        _store = store;
        _serializer = serializer;
    }

    public async Task<T?> ExecuteAsync<T>(
        IdempotencyKey key,
        string fingerprint,
        Func<CancellationToken, Task<T?>> operation,
        CancellationToken cancellationToken = default)
    {
        IdempotencyAcquireResult acquireResult = _store.TryAcquire(key, fingerprint);
        switch (acquireResult.Status)
        {
            case IdempotencyAcquireStatus.Acquired:
            {
                Guid ownerToken = acquireResult.OwnerToken
                                  ?? throw new InvalidOperationException("Acquired result has no owner token.");

                T? operationResult;

                try
                {
                    operationResult = await operation(cancellationToken);
                }
                catch
                {
                    _store.TryRelease(key, ownerToken);
                    throw;
                }

                byte[] payload = _serializer.Serialize(operationResult);

                if (!_store.TryComplete(key, ownerToken, payload))
                    throw new InvalidOperationException("Could not complete idempotent operation.");

                return operationResult;
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
}