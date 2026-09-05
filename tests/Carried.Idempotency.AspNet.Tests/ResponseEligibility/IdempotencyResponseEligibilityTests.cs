using System.Net;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Options;
using Carried.Idempotency.AspNet.Tests.TestServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Carried.Idempotency.AspNet.Tests.ResponseEligibility;

public sealed class IdempotencyResponseEligibilityTests
{
    [Fact]
    public async Task InternalServerError_IsNotRetained()
    {
        var executionCount = 0;

        await using WebApplication app = await CreateAppAsync(app =>
        {
            app.MapPost("/test", () =>
                {
                    executionCount++;

                    return Results.StatusCode(
                        StatusCodes.Status500InternalServerError);
                })
                .RequireIdempotency();
        });

        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstResponse =
            await SendRequestAsync(client);

        using HttpResponseMessage secondResponse =
            await SendRequestAsync(client);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            secondResponse.StatusCode);

        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task TooManyRequests_IsNotRetained()
    {
        var executionCount = 0;

        await using WebApplication app = await CreateAppAsync(app =>
        {
            app.MapPost("/test", () =>
                {
                    executionCount++;

                    return Results.StatusCode(
                        StatusCodes.Status429TooManyRequests);
                })
                .RequireIdempotency();
        });

        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstResponse =
            await SendRequestAsync(client);

        using HttpResponseMessage secondResponse =
            await SendRequestAsync(client);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            secondResponse.StatusCode);

        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task RequestTimeout_IsNotRetained()
    {
        var executionCount = 0;

        await using WebApplication app = await CreateAppAsync(app =>
        {
            app.MapPost("/test", () =>
                {
                    executionCount++;

                    return Results.StatusCode(
                        StatusCodes.Status408RequestTimeout);
                })
                .RequireIdempotency();
        });

        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstResponse =
            await SendRequestAsync(client);

        using HttpResponseMessage secondResponse =
            await SendRequestAsync(client);

        Assert.Equal(
            HttpStatusCode.RequestTimeout,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.RequestTimeout,
            secondResponse.StatusCode);

        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task ClientError_IsRetainedByDefault()
    {
        var executionCount = 0;

        await using WebApplication app = await CreateAppAsync(app =>
        {
            app.MapPost("/test", () =>
                {
                    executionCount++;

                    return Results.BadRequest();
                })
                .RequireIdempotency();
        });

        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstResponse =
            await SendRequestAsync(client);

        using HttpResponseMessage secondResponse =
            await SendRequestAsync(client);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            secondResponse.StatusCode);

        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ClientError_IsNotRetainedWhenDisabled()
    {
        var executionCount = 0;

        await using WebApplication app = await CreateAppAsync(
            app =>
            {
                app.MapPost("/test", () =>
                    {
                        executionCount++;

                        return Results.BadRequest();
                    })
                    .RequireIdempotency();
            },
            options => { options.DefaultPolicy.StoreClientErrors = false; });

        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstResponse =
            await SendRequestAsync(client);

        using HttpResponseMessage secondResponse =
            await SendRequestAsync(client);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            secondResponse.StatusCode);

        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task ResponseAtMaximumRetainedSize_IsRetained()
    {
        var executionCount = 0;

        const string responseBody = "12345678";

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost("/test", () =>
                        {
                            executionCount++;

                            return Results.Text(responseBody);
                        })
                        .RequireIdempotency();
                },
                configureAspNetOptions: options =>
                {
                    options.DefaultPolicy.MaxRetainedResponseBodySize =
                        responseBody.Length;
                });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "test-key");

        using HttpResponseMessage firstResponse =
            await server.Client.SendAsync(firstRequest);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "test-key");

        using HttpResponseMessage secondResponse =
            await server.Client.SendAsync(secondRequest);

        Assert.Equal(responseBody, await firstResponse.Content.ReadAsStringAsync());
        Assert.Equal(responseBody, await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ResponseOverMaximumRetainedSize_IsNotRetained()
    {
        var executionCount = 0;

        const string responseBody = "123456789";

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost("/test", () =>
                        {
                            executionCount++;

                            return Results.Text(responseBody);
                        })
                        .RequireIdempotency();
                },
                configureAspNetOptions: options => { options.DefaultPolicy.MaxRetainedResponseBodySize = 8; });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "test-key");

        using HttpResponseMessage firstResponse =
            await server.Client.SendAsync(firstRequest);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "test-key");

        using HttpResponseMessage secondResponse =
            await server.Client.SendAsync(secondRequest);

        Assert.Equal(responseBody, await firstResponse.Content.ReadAsStringAsync());
        Assert.Equal(responseBody, await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, executionCount);
    }

    private static async Task<WebApplication> CreateAppAsync(
        Action<WebApplication> configure,
        Action<IdempotencyAspNetOptions>? configureAspNetOptions = null)
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddIdempotency(
            configureAspNetOptions: configureAspNetOptions);

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseIdempotency();

        configure(app);

        await app.StartAsync();

        return app;
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            "test-key");

        return await client.SendAsync(request);
    }
}