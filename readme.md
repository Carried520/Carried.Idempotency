# Carried.Idempotency

A lightweight, storage-agnostic idempotency engine for .NET with ASP.NET Core integration and distributed Redis support.

## Packages

| Package | Description |
|---|---|
| `Carried.Idempotency` | Core idempotency engine |
| `Carried.Idempotency.DependencyInjection` | Dependency injection infrastructure for providers |
| `Carried.Idempotency.AspNet` | ASP.NET Core integration |
| `Carried.Idempotency.Redis` | Distributed Redis provider |

## 📥 Installation

### ASP.NET Core

```bash
dotnet add package Carried.Idempotency.AspNet
```

### Redis

```bash
dotnet add package Carried.Idempotency.Redis
```

### Core

For use without ASP.NET Core:

```bash
dotnet add package Carried.Idempotency
```

`Carried.Idempotency.DependencyInjection` is normally installed transitively and does not need to be installed directly.

## 🚀 Getting Started

### ASP.NET Core

Register idempotency:

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

Enable idempotency on an endpoint:

```csharp
app.MapPost("/orders", async () =>
{
    // Create order...
})
.RequireIdempotency();
```

Clients provide an idempotency key with the request:

```http
Idempotency-Key: request-123
```

Controllers can use the `RequireIdempotency` attribute instead:

```csharp
[RequireIdempotency]
[HttpPost]
public async Task<IActionResult> AddOrder()
{
    // Create order...
}
```

### Redis

For distributed applications, register an `IConnectionMultiplexer`:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
```

Then select Redis as the provider:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.UseRedis();
});
```

The application owns the `IConnectionMultiplexer` and its lifetime.

### Core

The Core engine can be used independently of ASP.NET Core:

```csharp
var idempotency = IdempotencyService.CreateInMemory();

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

## 💾 Storage Providers

| Provider | Package | Status |
|---|---|---|
| In-memory | `Carried.Idempotency` | ✅ Available |
| Redis | `Carried.Idempotency.Redis` | ✅ Available |
| Entity Framework Core | `Carried.Idempotency.EFCore` | 🛠️ Planned |

The in-memory provider is intended for single-process use. Redis provides distributed coordination across application instances.

Custom providers can be implemented using `IIdempotencyStore`.

## 📚 Documentation

See the project documentation for guides, configuration, and API reference.

**[Carried.Idempotency Documentation](https://carried520.github.io/Carried.Idempotency/)**

## 🧪 Example

An example ASP.NET Core application is available at:

```text
examples/Carried.Idempotency.ExampleApi
```

## 📋 License

Carried.Idempotency is licensed under the MIT License.

See [LICENSE](license.md) for details.
