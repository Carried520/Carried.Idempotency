# Carried.Idempotency

**Carried.Idempotency** is a lightweight, storage-agnostic idempotency engine for .NET with first-class ASP.NET Core integration.

It coordinates idempotent operations using idempotency keys, operation fingerprints, leases, and retained results, helping applications prevent duplicate execution and safely replay completed operations.

## Features

- **Storage-agnostic Core** — use the built-in in-memory store or provide your own implementation of `IIdempotencyStore`.
- **Lease-based ownership** — coordinates concurrent execution and prevents stale owners from completing operations.
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
| `Carried.Idempotency.AspNet` | ASP.NET Core integration built on top of the Core engine. |

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
})
.RequireIdempotency();
```

Clients provide an idempotency key using the `Idempotency-Key` request header.

```http
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

A repeated request using the same key and request fingerprint can receive the previously retained response instead of executing the endpoint again.

Reusing the same key for a different request results in an idempotency conflict.

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

## How It Works

When an operation is executed with an idempotency key:

1. **New key** — ownership is acquired and the operation executes.
2. **Active key** — another execution using the same key and operation is reported as already in progress.
3. **Completed key** — the previously retained result is replayed.
4. **Conflicting key** — reuse of the key for a different operation is rejected.
5. **Expired key** — the key can be acquired again.

While an operation owns a key, its lease is automatically renewed. If ownership is lost before completion, the operation cannot overwrite the state of a newer owner.

## Documentation

Use the documentation and API reference to learn more about:

- Core idempotency semantics
- ASP.NET Core integration
- Policies
- Request fingerprinting
- Response replay
- Custom stores
- Custom serialization
- Observability

The **API Reference** contains documentation for the complete public API generated directly from the library's C# XML documentation.

## License

Carried.Idempotency is licensed under the MIT License.