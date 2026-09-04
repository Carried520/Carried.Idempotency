using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.AspNet.Tests;

internal sealed class IdempotencyTestServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private IdempotencyTestServer(
        WebApplication app,
        HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public static async Task<IdempotencyTestServer> CreateAsync(
        Action<IEndpointRouteBuilder> configureEndpoints,
        Action<IServiceCollection>? configureServices = null)
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddIdempotency();

        configureServices?.Invoke(builder.Services);

        WebApplication app = builder.Build();

        app.UseIdempotency();

        configureEndpoints(app);

        await app.StartAsync();

        HttpClient client =
            app.GetTestClient();

        return new IdempotencyTestServer(
            app,
            client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}