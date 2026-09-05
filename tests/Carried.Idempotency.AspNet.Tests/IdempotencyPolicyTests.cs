using Carried.Idempotency.AspNet.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyPolicyTests
{
    [Fact]
    public async Task DefaultPolicy_IsUsed_WhenNoPolicyNameIsSpecified()
    {
        await using var server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.StatusCode(
                                StatusCodes.Status400BadRequest))
                        .RequireIdempotency();
                },
                configureAspNetOptions: options => { options.DefaultPolicy.StoreClientErrors = false; });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "default-policy-key");

        HttpResponseMessage firstResponse =
            await server.Client.SendAsync(firstRequest);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "default-policy-key");

        HttpResponseMessage secondResponse =
            await server.Client.SendAsync(secondRequest);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (int)firstResponse.StatusCode);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (int)secondResponse.StatusCode);
    }

    [Fact]
    public async Task NamedPolicy_IsUsed_ForMinimalApiEndpoint()
    {
        var executionCount = 0;

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () =>
                            {
                                executionCount++;

                                return Results.StatusCode(
                                    StatusCodes.Status400BadRequest);
                            })
                        .RequireIdempotency("payments");
                },
                configureAspNetOptions: options =>
                {
                    options.DefaultPolicy.StoreClientErrors = true;

                    options.AddPolicy(
                        "payments",
                        policy => { policy.StoreClientErrors = false; });
                });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "named-policy-key");

        await server.Client.SendAsync(firstRequest);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "named-policy-key");

        await server.Client.SendAsync(secondRequest);

        Assert.Equal(2, executionCount);
    }

    [Fact]
    public async Task NamedPolicy_MaxKeyLength_IsUsed()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.Ok())
                        .RequireIdempotency("short-keys");
                },
                configureAspNetOptions: options =>
                {
                    options.DefaultPolicy.MaxKeyLength = 255;

                    options.AddPolicy(
                        "short-keys",
                        policy => { policy.MaxKeyLength = 5; });
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            "123456");

        HttpResponseMessage response =
            await server.Client.SendAsync(request);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (int)response.StatusCode);
    }

    [Fact]
    public async Task UnknownPolicy_ReturnsInternalServerError()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.Ok())
                        .RequireIdempotency("missing");
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            "test-key");

        HttpResponseMessage response =
            await server.Client.SendAsync(request);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            (int)response.StatusCode);
    }
}