# Getting Started

Carried.Idempotency provides a storage-agnostic idempotency engine for .NET and an
optional ASP.NET Core integration for HTTP APIs.

This guide covers the quickest way to start using both packages.

## ASP.NET Core

For ASP.NET Core applications, install:

```bash
dotnet add package Carried.Idempotency.AspNet
```

The ASP.NET Core package integrates the Core idempotency engine with the HTTP
request pipeline.

### Register idempotency

Register idempotency services during application startup and select a storage
provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();
});
```

The in-memory provider is useful when idempotency state only needs to exist within
a single application process.

### Add the middleware

Add the idempotency middleware to the application pipeline:

```csharp
app.UseIdempotency();
```

`UseIdempotency()` should be registered after routing so endpoint metadata is
available to the middleware.

Only endpoints explicitly configured to require idempotency are processed by the
middleware.

### Protect a Minimal API endpoint

Use `RequireIdempotency()` to enable idempotency for an endpoint:

```csharp
app.MapPost("/orders", async () =>
{
    // Create the order.

    return Results.Ok();
})
.RequireIdempotency();
```

Clients calling this endpoint must provide an idempotency key using the
`Idempotency-Key` request header:

```http
POST /orders
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

If the request completes successfully, its response can be retained for replay.

Sending the same request again with the same idempotency key can replay the
retained response without executing the endpoint again.

### Protect a controller endpoint

Controller actions can opt into idempotency using `RequireIdempotency`:

```csharp
[RequireIdempotency]
[HttpPost]
public async Task<IActionResult> CreateOrder()
{
    // Create the order.

    return Ok();
}
```

The same idempotency semantics apply to Minimal APIs and controllers.

## Request behavior

For an idempotency-enabled endpoint, the middleware associates the supplied
idempotency key with a fingerprint of the request.

This allows the middleware to distinguish between a legitimate retry and reuse of
the same key for a different request.

| Situation | Behavior |
| --- | --- |
| New key | The endpoint executes |
| Same key and same request while active | The operation is already in progress |
| Same key and same completed request | The retained response is replayed |
| Same key and different request | An idempotency conflict is returned |
| Expired entry | The key can be acquired again |

A missing or invalid idempotency key on an endpoint that requires idempotency
results in a `400 Bad Request`.

## Configure the engine

The Core engine can be configured when registering ASP.NET Core idempotency:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.LeaseDuration = TimeSpan.FromMinutes(5);
    options.CompletedRetention = TimeSpan.FromHours(24);

    options.UseInMemory();
});
```

### Lease duration

`LeaseDuration` controls how long an acquired idempotency key remains owned without
a successful lease renewal.

The default is **5 minutes**.

While an operation is executing, the engine renews its lease. This prevents another
execution from acquiring the key while the current owner remains active.

### Completed retention

`CompletedRetention` controls how long a completed result remains available for
replay.

The default is **24 hours**.

After the completed entry expires, the idempotency key can be acquired again.

## Using the Core package directly

Applications that do not need ASP.NET Core integration can use
`Carried.Idempotency` directly.

Install the Core package:

```bash
dotnet add package Carried.Idempotency
```

For an in-memory setup:

```csharp
var idempotency = IdempotencyService.CreateInMemory();
```

Create an idempotency key:

```csharp
var key = new IdempotencyKey(
    scope: "orders",
    value: "550e8400-e29b-41d4-a716-446655440000");
```

The scope identifies where the key is unique. This allows the same key value to be
used independently in different scopes.

Execute an idempotent operation:

```csharp
var result = await idempotency.ExecuteAsync(
    key,
    fingerprint: "create-order",
    async cancellationToken =>
    {
        var order = await CreateOrderAsync(cancellationToken);

        return IdempotencyOperationResult<Order>.Complete(order);
    });
```

The fingerprint identifies the operation associated with the key. Reusing an
active key with a different fingerprint results in an idempotency conflict.

## Complete or release an operation

An operation explicitly determines whether its result should be retained.

### Complete

Return `Complete` when the result should be retained for future replay:

```csharp
return IdempotencyOperationResult<Order>.Complete(order);
```

A later execution using the same key and fingerprint receives the retained result
instead of executing the operation again.

### Release

Return `Release` when the result should be returned to the current caller without
being retained:

```csharp
return IdempotencyOperationResult<Order>.Release(order);
```

Ownership of the idempotency key is released, allowing a subsequent execution to
acquire the key and execute the operation again.

## Core exceptions

When using the Core engine directly, idempotency conditions are represented by
specific exceptions:

- `IdempotencyInProgressException` — another operation currently owns the key.
- `IdempotencyConflictException` — the key is associated with a different operation.
- `IdempotencyLeaseLostException` — ownership was lost before the operation could
  be completed.

ASP.NET Core integration handles these engine conditions and translates them into
HTTP responses.

## Next steps

Once the basic integration is working, continue with the relevant guides:

- **Idempotency Semantics** — understand ownership, leases, completion, and replay.
- **ASP.NET Core Policies** — customize behavior globally or for individual endpoints.
- **Request Fingerprinting** — understand how requests are identified and extend fingerprints.
- **Response Replay** — configure which responses and headers can be retained.
- **Custom Stores** — implement a persistent or distributed `IIdempotencyStore`.
- **Observability** — integrate lifecycle events, logging, and metrics.

For individual types and members, see the **API Reference**.