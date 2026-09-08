using Carried.Idempotency.Options;
using Carried.Idempotency.Store;
using Microsoft.EntityFrameworkCore;

namespace Carried.Idempotency.EntityFrameworkCore.Store;

internal sealed class EntityFrameworkCoreStore<TContext> : IIdempotencyStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly IdempotencyOptions _options;
    private readonly TimeProvider _timeProvider;

    private const int MaxAcquireAttempts = 3;
    
    public EntityFrameworkCoreStore(TContext context,
        IdempotencyOptions options,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<IdempotencyAcquireResult> TryAcquireAsync(
        IdempotencyKey key,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        var ownerToken = Guid.NewGuid();

        for (var attempt = 0; attempt < MaxAcquireAttempts; attempt++)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            IdempotencyEntry? entry = await _context.Set<IdempotencyEntry>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Scope == key.Scope &&
                         x.Key == key.Value,
                    cancellationToken);

            if (entry is null)
            {
                var candidate = new IdempotencyEntry
                {
                    Scope = key.Scope,
                    Key = key.Value,
                    Fingerprint = fingerprint,
                    OwnerToken = ownerToken,
                    State = IdempotencyEntryState.InProgress,
                    Payload = null,
                    ExpiresAt = now + _options.LeaseDuration
                };

                try
                {
                    _context.Add(candidate);
                    await _context.SaveChangesAsync(cancellationToken);

                    return IdempotencyAcquireResult.Acquired(ownerToken);
                }
                catch (DbUpdateException)
                {
                    _context.Entry(candidate).State = EntityState.Detached;

                    entry = await _context.Set<IdempotencyEntry>()
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            x => x.Scope == key.Scope &&
                                 x.Key == key.Value,
                            cancellationToken);

                    if (entry is null)
                        throw;

                    now = _timeProvider.GetUtcNow().UtcDateTime;
                }
            }

            if (entry.ExpiresAt <= now)
            {
                int affected = await _context.Set<IdempotencyEntry>()
                    .Where(x =>
                        x.Scope == key.Scope &&
                        x.Key == key.Value &&
                        x.ExpiresAt <= now)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.Fingerprint, fingerprint)
                            .SetProperty(x => x.OwnerToken, ownerToken)
                            .SetProperty(x => x.State, IdempotencyEntryState.InProgress)
                            .SetProperty(x => x.Payload, (byte[]?)null)
                            .SetProperty(
                                x => x.ExpiresAt,
                                now + _options.LeaseDuration),
                        cancellationToken);

                if (affected == 1)
                    return IdempotencyAcquireResult.Acquired(ownerToken);
                continue;
            }

            if (entry.Fingerprint != fingerprint)
                return IdempotencyAcquireResult.Conflict();

            return entry.State switch
            {
                IdempotencyEntryState.InProgress =>
                    IdempotencyAcquireResult.InProgress(),

                IdempotencyEntryState.Completed =>
                    IdempotencyAcquireResult.Completed(
                        entry.Payload ?? throw new InvalidOperationException(
                            "Completed idempotency entry has no payload.")),

                _ => throw new InvalidOperationException($"Unknown idempotency entry state '{entry.State}'.")
            };
        }

        throw new InvalidOperationException("Failed to acquire idempotency entry due to concurrent modifications.");
    }

    public async ValueTask<bool> TryCompleteAsync(IdempotencyKey key,
        Guid ownerToken,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        int affected = await _context.Set<IdempotencyEntry>()
            .Where(x =>
                x.Scope == key.Scope &&
                x.Key == key.Value &&
                x.State == IdempotencyEntryState.InProgress &&
                x.OwnerToken == ownerToken &&
                x.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.State, IdempotencyEntryState.Completed)
                    .SetProperty(x => x.OwnerToken, (Guid?)null)
                    .SetProperty(x => x.Payload, payload)
                    .SetProperty(
                        x => x.ExpiresAt,
                        now + _options.CompletedRetention),
                cancellationToken);

        return affected == 1;
    }

    public async ValueTask<bool> TryReleaseAsync(IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        int affected = await _context.Set<IdempotencyEntry>()
            .Where(x =>
                x.Scope == key.Scope &&
                x.Key == key.Value &&
                x.State == IdempotencyEntryState.InProgress &&
                x.OwnerToken == ownerToken &&
                x.ExpiresAt > now)
            .ExecuteDeleteAsync(cancellationToken);

        return affected == 1;
    }

    public async ValueTask<bool> TryRenewLeaseAsync(IdempotencyKey key,
        Guid ownerToken,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        int affected = await _context.Set<IdempotencyEntry>()
            .Where(x =>
                x.Scope == key.Scope &&
                x.Key == key.Value &&
                x.State == IdempotencyEntryState.InProgress &&
                x.OwnerToken == ownerToken &&
                x.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ExpiresAt, now + _options.LeaseDuration),
                cancellationToken);

        return affected == 1;
    }

    private static void ValidateKey(IdempotencyKey key)
    {
        if (key.Scope.Length > IdempotencyEntry.MaxKeyPartLength)
            throw new ArgumentException(
                $"Idempotency scope cannot exceed {IdempotencyEntry.MaxKeyPartLength} characters.",
                nameof(key));

        if (key.Value.Length > IdempotencyEntry.MaxKeyPartLength)
            throw new ArgumentException(
                $"Idempotency key cannot exceed {IdempotencyEntry.MaxKeyPartLength} characters.",
                nameof(key));
    }
}