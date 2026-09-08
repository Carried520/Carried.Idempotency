# Carried.Idempotency

**Carried.Idempotency** is a lightweight, storage-agnostic idempotency engine for .NET with ASP.NET Core integration and distributed Redis support.

It coordinates idempotent operations using idempotency keys, operation fingerprints, leases, and retained results, helping applications prevent duplicate execution, coordinate concurrent requests, and safely replay completed operations.

## Features

- **Storage-agnostic Core** — use the built-in in-memory store, the Redis provider, or implement `IIdempotencyStore` for another backend.
- **Distributed Redis provider** — coordinate idempotency across application instances using atomic Redis operations.
- **Lease-based ownership** — coordinates concurrent execution and prevents stale owners from completing, releasing, or renewing operations they no longer own.
- **Result replay** — completed operation results can be retained and returned without executing the operation again.
- **Conflict detection** — detects when an idempotency key is reused for a different operation.
- **ASP.NET Core integration** — protect Minimal API endpoints or controllers with opt-in idempotency.
- **Request fingerprinting** — prevents the same key from being reused for a different HTTP request.
- **Configurable policies** — customize idempotency behavior globally or per endpoint.
- **Extensible fingerprinting** — contribute application-specific data to request fingerprints.
- **Observability** — lifecycle events, logging, and metrics for monitoring idempotency behavior.
- **Extensible storage and serialization** — implement custom stores and serializers without coupling the Core engine to infrastructure.

## Packages

| Package | Description |
| --- | --- |
| `Carried.Idempotency` | Core idempotency engine with no ASP.NET Core dependency. |
| `Carried.Idempotency.DependencyInjection` | Dependency injection infrastructure for configuring idempotency providers. |
| `Carried.Idempotency.AspNet` | ASP.NET Core integration built on top of the Core engine. |
| `Carried.Idempotency.Redis` | Redis provider for distributed idempotency coordination. |

## Quick Start

### ASP.NET Core

Install the ASP.NET Core package:

```bash
dotnet add package Carried.Idempotency.AspNet
```

Register idempotency and choose a storage provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();
});
```

Add the middleware:

```csharp
app.UseIdempotency();
```

Then opt an endpoint into idempotency:

```csharp
app.MapPost("/orders", async () =>
{
    // Perform the operation.

    return Results.Ok();
})
.RequireIdempotency();
```

Clients provide an idempotency key using the `Idempotency-Key` request header:

```http
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

A repeated request using the same key and request fingerprint can receive the previously retained response instead of executing the endpoint again.

Reusing the same key for a different request results in an idempotency conflict.

> The in-memory provider stores idempotency state within a single application process.
> For applications running across multiple instances, use a distributed provider such as Redis.

## Redis

Install the Redis provider:

```bash
dotnet add package Carried.Idempotency.Redis
```

Register a long-lived `IConnectionMultiplexer` with the application:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
```

Then select Redis as the idempotency provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseRedis();
});
```

`UseRedis()` resolves the registered `IConnectionMultiplexer` and obtains an `IDatabase` from it.

The application owns the connection multiplexer and its lifetime. `Carried.Idempotency.Redis` does not dispose it.

Redis-specific configuration can be supplied when selecting the provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseRedis(redis =>
    {
        redis.KeyPrefix = "my-app:idempotency:";
    });
});
```

The default Redis key prefix is:

```text
carried:idempotency:
```

Applications sharing a Redis database can use different prefixes to isolate their idempotency state.

The Redis provider performs its state transitions atomically and uses Redis server time for lease and expiration decisions.

## Core

`Carried.Idempotency` can also be used independently of ASP.NET Core.

```csharp
var idempotency = IdempotencyService.CreateInMemory();

var key = new IdempotencyKey(
    scope: "orders",
    value: "550e8400-e29b-41d4-a716-446655440000");

var result = await idempotency.ExecuteAsync(
    key,
    fingerprint: "create-order",
    async cancellationToken =>
    {
        var order = await CreateOrderAsync(cancellationToken);

        return IdempotencyOperationResult<Order>.Complete(order);
    });
```

Returning `Complete` retains the result for future replay:

```csharp
return IdempotencyOperationResult<T>.Complete(value);
```

Returning `Release` returns the result without retaining it and releases ownership of the key:

```csharp
return IdempotencyOperationResult<T>.Release(value);
```

The Core engine can also use Redis directly:

```csharp
IConnectionMultiplexer connection = /* ... */;
IDatabase database = connection.GetDatabase();

var options = new IdempotencyOptions
{
    LeaseDuration = TimeSpan.FromMinutes(5),
    CompletedRetention = TimeSpan.FromHours(24)
};

var idempotency = IdempotencyService.CreateRedis(
    database,
    options);
```

## How It Works

When an operation is executed with an idempotency key:

1. **New key** — ownership is acquired and the operation executes.
2. **Active key** — another execution using the same key and fingerprint is reported as already in progress.
3. **Completed key** — the previously retained result is replayed.
4. **Conflicting key** — reuse of an active or retained key for a different operation is rejected.
5. **Expired key** — the key can be acquired again.

While an operation owns a key, its lease is automatically renewed. Ownership is represented by an owner token, allowing the configured store to reject state transitions from stale owners.

If ownership is lost before the requested state transition can be applied, the operation cannot overwrite the state of a newer owner.

## Storage Providers

| Provider | Package | Use case |
| --- | --- | --- |
| In-memory | `Carried.Idempotency` | Single-process applications, development, and testing |
| Redis | `Carried.Idempotency.Redis` | Distributed applications and multiple application instances |

Additional providers can be implemented using `IIdempotencyStore`.

## Documentation

Use the documentation and API reference to learn more about:

- Getting started
- Core idempotency semantics
- ASP.NET Core integration
- Redis
- Policies
- Request fingerprinting
- Response replay
- Custom stores
- Custom serialization
- Observability

The **API Reference** contains documentation for the complete public API generated directly from the library's C# XML documentation.

## License

Carried.Idempotency is licensed under the MIT License.