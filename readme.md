# Carried.Idempotency

## About

A lightweight, storage-agnostic idempotency engine for .NET with ASP.NET Core integration.

`Carried.Idempotency` provides primitives for coordinating idempotent operations using
idempotency keys, operation fingerprints, leases, and retained results.

The project consists of:

- **Carried.Idempotency** — Core idempotency engine with no ASP.NET Core dependency.
- **Carried.Idempotency.AspNet** — ASP.NET Core integration built on top of the Core engine.

## 📥 Installation

### Core

```bash
dotnet add package Carried.Idempotency
```

NuGet: [Carried.Idempotency](TODO_NUGET_LINK)

### ASP.NET Core

```bash
dotnet add package Carried.Idempotency.AspNet
```

NuGet: [Carried.Idempotency.AspNet](TODO_NUGET_LINK)

## 🚀 Getting Started

### Core

Create an in-memory idempotency service:

```csharp
var idempotency = IdempotencyService.CreateInMemory();
```

Execute an operation using an idempotency key and operation fingerprint:

```csharp
var key = new IdempotencyKey(
    scope: "orders-create",
    value: "request-123");

var result = await idempotency.ExecuteAsync(
    key,
    fingerprint: "my-fingerprint-here",
    async cancellationToken =>
    {
        // Perform operation.

        return IdempotencyOperationResult<string>.Complete("result");
    },
    cancellationToken);
```

A successfully completed operation is retained and can be replayed when the same
idempotency key and fingerprint are used again.

## 🔁 Operation Results

An idempotent operation explicitly determines what should happen to its result.

### Complete

```csharp
return IdempotencyOperationResult<T>.Complete(value);
```

`Complete` retains the result for replay. A subsequent execution using the same
idempotency key and fingerprint receives the retained result instead of executing
the operation again.

### Release

```csharp
return IdempotencyOperationResult<T>.Release(value);
```

`Release` returns the value without retaining it for replay and releases ownership
of the idempotency key.

A subsequent execution can acquire the key and execute the operation again.

## 🔑 Idempotency Behavior

| Situation                           | Behavior                                         |
|-------------------------------------|--------------------------------------------------|
| New idempotency key                 | Ownership is acquired and the operation executes |
| Same key and operation while active | Operation is already in progress                 |
| Same key and completed operation    | Previously retained result is replayed           |
| Same key for a different operation  | Conflict                                         |
| Expired entry                       | The key can be acquired again                    |

### Exceptions

The Core engine can report the following idempotency conditions:

- `IdempotencyInProgressException` — another operation currently owns the key.
- `IdempotencyConflictException` — the key is associated with a different operation.
- `IdempotencyLeaseLostException` — ownership was lost before the operation could be completed.

## ⚙️ Core Configuration

Core behavior can be configured using `IdempotencyOptions`:

```csharp
var options = new IdempotencyOptions
{
    LeaseDuration = TimeSpan.FromMinutes(5),
    CompletedRetention = TimeSpan.FromHours(24)
};

var idempotency = IdempotencyService.CreateInMemory(options);
```

### LeaseDuration

Controls how long an acquired idempotency key remains owned without a successful
lease renewal.

Default:

```text
5 minutes
```

### CompletedRetention

Controls how long a completed result remains available for replay.

Default:

```text
24 hours
```

## 🌐 ASP.NET Core

`Carried.Idempotency.AspNet` provides HTTP integration for the Core engine.

### Registration

```csharp
builder.Services.AddIdempotency(options =>
{
    options.LeaseDuration = TimeSpan.FromMinutes(5);
    options.CompletedRetention = TimeSpan.FromHours(24);

    options.UseInMemory();
    options.HeaderName = "Idempotency-Key";
});
```

Add the idempotency middleware:

```csharp
app.UseIdempotency();
```

> `UseIdempotency()` should be registered after routing.

### Minimal APIs

Enable idempotency for an endpoint:

```csharp
app.MapPost("/orders/add", async () =>
{
    // ...
})
.RequireIdempotency();
```

### Controllers

Idempotency can also be enabled using the `RequireIdempotency` attribute:

```csharp
[RequireIdempotency]
[HttpPost]
public async Task<IActionResult> AddOrder()
{
    // ...
}
```

Only endpoints explicitly configured to require idempotency are processed by the
idempotency middleware.

## 📨 Idempotency Key

By default, clients provide the idempotency key using:

```http
Idempotency-Key: <key>
```

The header name can be configured:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.HeaderName = "X-Idempotency-Key";
    options.UseInMemory();
});
```

Missing or invalid idempotency keys on protected endpoints result in a `400 Bad Request`.

## 🧬 Request Fingerprinting

ASP.NET Core requests are fingerprinted so that an idempotency key cannot be reused
for a different operation.

The default fingerprint includes relevant request information such as:

- HTTP method
- Route pattern
- Route values
- Query parameters
- Content type
- Request body

Custom fingerprint inputs can be added by implementing:

```csharp
IIdempotencyFingerprintContributor
```

## 📜 Response Replay

Completed HTTP responses can be retained and replayed for subsequent requests using
the same idempotency key and request fingerprint.

Safe response headers are replayed along with the stored response.

By default, replayable headers include:

- `Location`
- `ETag`
- `Cache-Control`
- `Last-Modified`

Additional replay headers can be configured if required.

> Streaming responses and Server-Sent Events (`text/event-stream`) are not supported
> for idempotent response capture.

## 📏 Policies

The default ASP.NET Core idempotency behavior can be configured through the default
policy.

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();

    options.DefaultPolicy.MaxKeyLength = 255;
});
```

Named policies can be registered:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseInMemory();

    options.AddPolicy("strict", policy =>
    {
        policy.MaxKeyLength = 255;
    });
});
```

A named policy can then be applied to an endpoint:

```csharp
app.MapPost("/orders/add", async () =>
{
    // ...
})
.RequireIdempotency("strict");
```

or on controller:

```csharp
[RequireIdempotency("strict")]
[HttpPost]
public async Task<IActionResult> AddOrder()
{
    // ...
}
```

## Storage Providers

The Core package includes an in-memory store, with built-in ASP.NET Core registration
available through `UseInMemory()`. Additional storage providers are planned.

| Provider              | Package                      | Status      |
|-----------------------|------------------------------|-------------|
| In-memory             | `Carried.Idempotency`        | ✅ Available |
| Redis                 | `Carried.Idempotency.Redis`  | 🛠️ Planned |
| Entity Framework Core | `Carried.Idempotency.EFCore` | 🛠️ Planned |

## 🔌 Custom Stores

The Core engine is storage-agnostic. Custom stores can be used directly with the
Core engine by implementing:

```csharp
IIdempotencyStore
```

A store is responsible for atomically coordinating:

- Key acquisition
- Completion
- Release
- Lease renewal
- Conflict detection
- Completed-result replay

Custom stores must preserve the ownership and lease semantics defined by
`IIdempotencyStore`.

```csharp
public sealed class MyIdempotencyStore : IIdempotencyStore
{
    // implement IIdempotencyStore methods here
}
```

## 🔄 Custom Serialization

The Core engine uses `IIdempotencySerializer` to serialize values retained for replay.

A custom serializer can be supplied when creating an `IdempotencyService`:

```csharp
var idempotency = IdempotencyService.Create(
    store,
    serializer,
    options);
```

Custom serializers implement:

```csharp
IIdempotencySerializer
```

## 🔎 Observability

The Core engine exposes lifecycle events for observing idempotency behavior:

- Acquired
- In progress
- Replayed
- Conflict
- Completed
- Released
- Release failed
- Lease lost

ASP.NET Core integration can additionally provide logging and metrics.

```csharp
builder.Services.AddIdempotencyLogging();
builder.Services.AddIdempotencyMetrics();
```

## 🧪 Example

An example ASP.NET Core application is available in:

```text
examples/Carried.Idempotency.ExampleApi
```

It demonstrates the package using actual HTTP requests and idempotency behavior.

## 📋 License

This project is licensed under the MIT License.

See [LICENSE](license.md) for details.

## 📦 Semantic Versioning (SemVer)

This project follows [Semantic Versioning](https://semver.org/) using the
`MAJOR.MINOR.PATCH` version format.

- **Patch** — Backward-compatible bug fixes.
- **Minor** — New backward-compatible functionality.
- **Major** — Incompatible API changes.

## 🏷️ Branches

- **Main** — Contains the latest stable release.
- **Dev** — Contains changes intended for the next release.
- **Feature** — `feature/*` branches contain individual features and are merged into `dev` when completed.
- **Bugfix** — `bugfix/*` branches contain individual bug fixes and are merged into `dev` when completed.