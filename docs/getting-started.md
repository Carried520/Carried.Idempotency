# Getting Started

Carried.Idempotency provides a storage-agnostic idempotency engine for .NET with ASP.NET Core integration and multiple persistence providers.

This guide covers the quickest way to start using Carried.Idempotency in an ASP.NET Core application or directly through the Core API.

## ASP.NET Core

Install the ASP.NET Core package:

```bash
dotnet add package Carried.Idempotency.AspNet
```

The ASP.NET Core package integrates the Core idempotency engine with the HTTP request pipeline.

### Register idempotency

Register idempotency services during application startup and select a storage provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();
});
```

The in-memory provider keeps idempotency state within a single application process.

It is useful for development, testing, and applications where idempotency coordination does not need to span multiple application instances.

For applications that require shared persistence across application instances, use the Redis or Entity Framework Core providers described later in this guide.

### Add the middleware

Add the idempotency middleware to the application pipeline:

```csharp
app.UseIdempotency();
```

`UseIdempotency()` should be registered after routing so endpoint metadata is available to the middleware.

Only endpoints explicitly configured to require idempotency are processed by the middleware.

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

Clients calling this endpoint must provide an idempotency key using the `Idempotency-Key` request header:

```http
POST /orders
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
```

If the request completes successfully, its response can be retained for replay.

Sending the same request again with the same idempotency key and request fingerprint can replay the retained response without executing the endpoint again.

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

For an idempotency-enabled endpoint, the middleware associates the supplied idempotency key with a fingerprint of the request.

This allows the middleware to distinguish between a legitimate retry and reuse of the same key for a different request.

| Situation | Behavior |
| --- | --- |
| New key | The endpoint executes |
| Same key and same request while active | The operation is already in progress |
| Same key and same completed request | The retained response is replayed |
| Same key and different request | An idempotency conflict is returned |
| Expired entry | The key can be acquired again |

A missing or invalid idempotency key on an endpoint that requires idempotency results in a `400 Bad Request`.

## Configure the engine

Core behavior can be configured when registering ASP.NET Core idempotency:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.LeaseDuration = TimeSpan.FromMinutes(5);
    options.CompletedRetention = TimeSpan.FromHours(24);

    options.UseInMemory();
});
```

### Lease duration

`LeaseDuration` controls how long an acquired idempotency key remains owned without a successful lease renewal.

The default is **5 minutes**.

While an operation is executing, the engine periodically renews its lease. This prevents another execution from acquiring the key while the current owner remains active.

If the lease expires and another execution acquires the key, the previous owner becomes stale and cannot successfully complete, release, or renew the new ownership state.

### Completed retention

`CompletedRetention` controls how long a completed result remains available for replay.

The default is **24 hours**.

After the completed entry expires, the idempotency key can be acquired again.

## Using Redis

For applications where idempotency state must be coordinated through Redis, install the Redis provider:

```bash
dotnet add package Carried.Idempotency.Redis
```

### Register the Redis connection

Register a long-lived `IConnectionMultiplexer` with the application:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
```

Then select Redis when configuring idempotency:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseRedis();
});
```

`UseRedis()` resolves `IConnectionMultiplexer` from the application's service provider and obtains an `IDatabase` from it.

The application owns the `IConnectionMultiplexer` and its lifetime. The Redis idempotency provider does not create or dispose the multiplexer.

### Configure the Redis key prefix

Redis keys use the following prefix by default:

```text
carried:idempotency:
```

A custom prefix can be configured:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseRedis(redis =>
    {
        redis.KeyPrefix = "orders-api:idempotency:";
    });
});
```

Use different prefixes when applications share the same Redis database but should not share idempotency state.

### Supply an IDatabase directly

Applications that need explicit control over database selection can supply an `IDatabase`:

```csharp
IConnectionMultiplexer connection = /* ... */;
IDatabase database = connection.GetDatabase();

builder.Services.AddIdempotency(options =>
{
    options.UseRedis(database);
});
```

### Redis timing requirements

The Redis provider represents lease and retention durations in milliseconds.

Both `LeaseDuration` and `CompletedRetention` must therefore be at least one millisecond.

The provider performs state transitions atomically and uses Redis server time when evaluating leases and expiration.

## Using Entity Framework Core

For applications that want to persist idempotency state through an existing relational database, install the Entity Framework Core provider:

```bash
dotnet add package Carried.Idempotency.EntityFrameworkCore
```

The application must also reference and configure the Entity Framework Core database provider it uses, such as PostgreSQL or SQL Server.

### Register the DbContext

Register the application's `DbContext` normally:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Configure the application's database provider.
});
```

Then select the Entity Framework Core idempotency provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseDbContext<AppDbContext>();
});
```

The `DbContext` remains owned by the application's dependency injection container. Carried.Idempotency uses the registered context rather than creating or managing one itself.

### Add the idempotency model

The idempotency entity must be included in the application's EF Core model.

Call `AddIdempotency()` from `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.AddIdempotency();
}
```

The idempotency table is then managed through the application's normal EF Core migrations:

```bash
dotnet ef migrations add AddIdempotency
dotnet ef database update
```

By default, the table is named:

```text
__CarriedIdempotency
```

### Configure the table

The table name and schema can be customized when registering the model:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.AddIdempotency(options =>
    {
        options.TableName = "IdempotencyEntries";
        options.Schema = "infrastructure";
    });
}
```

Because this configuration is part of the EF Core model, changes to the table name or schema should be applied through the application's migrations.

### Key length

The Entity Framework Core provider stores the idempotency scope and key as a composite primary key.

Both the scope and key are limited to **256 characters**. Values exceeding this limit are rejected before a database operation is performed.

### Entity Framework Core provider behavior

The provider uses conditional database updates and deletes to preserve idempotency ownership and lease semantics.

A stale owner cannot complete, release, or renew an entry after its lease expires or another execution takes ownership.

The provider is intended for relational Entity Framework Core providers.

## Using the Core package directly

Applications that do not need ASP.NET Core integration can use `Carried.Idempotency` directly.

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

The scope identifies where the key is unique. This allows the same key value to be used independently in different scopes.

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

The fingerprint identifies the operation associated with the key. Reusing an active or retained key with a different fingerprint results in an idempotency conflict.

### Core with Redis

Redis can also back the Core service directly without ASP.NET Core:

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

### Core with Entity Framework Core

Entity Framework Core can also back the Core service directly without dependency injection:

```csharp
await using var context = new AppDbContext(/* ... */);

var idempotency = IdempotencyService.CreateEfCore(context);
```

The context model must still include the idempotency configuration:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.AddIdempotency();
}
```

The caller retains ownership of the `DbContext` and is responsible for its lifetime.

Custom engine options can also be supplied:

```csharp
var options = new IdempotencyOptions
{
    LeaseDuration = TimeSpan.FromMinutes(5),
    CompletedRetention = TimeSpan.FromHours(24)
};

var idempotency = IdempotencyService.CreateEfCore(
    context,
    options);
```

## Complete or release an operation

An operation explicitly determines whether its result should be retained.

### Complete

Return `Complete` when the result should be retained for future replay:

```csharp
return IdempotencyOperationResult<Order>.Complete(order);
```

A later execution using the same key and fingerprint receives the retained result instead of executing the operation again.

### Release

Return `Release` when the result should be returned to the current caller without being retained:

```csharp
return IdempotencyOperationResult<Order>.Release(order);
```

Ownership of the idempotency key is released, allowing a subsequent execution to acquire the key and execute the operation again.

## Core exceptions

When using the Core engine directly, idempotency conditions are represented by specific exceptions:

- `IdempotencyInProgressException` — another operation currently owns the key.
- `IdempotencyConflictException` — the key is associated with a different operation.
- `IdempotencyLeaseLostException` — ownership was lost before the requested state transition could be applied.

ASP.NET Core integration handles these engine conditions and translates them into HTTP responses.

## Next steps

Once the basic integration is working, continue with the relevant guides:

- **Idempotency Semantics** — understand ownership, leases, completion, release, and replay.
- **ASP.NET Core Policies** — customize behavior globally or for individual endpoints.
- **Request Fingerprinting** — understand how requests are identified and extend fingerprints.
- **Response Replay** — configure which responses and headers can be retained.
- **Redis** — configure Redis-backed idempotency coordination.
- **Entity Framework Core** — configure relational database-backed idempotency.
- **Custom Stores** — implement another `IIdempotencyStore` provider.
- **Observability** — integrate lifecycle events, logging, and metrics.

For individual types and members, see the **API Reference**.