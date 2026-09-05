using System.Net;
using Carried.Idempotency.AspNet.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyKeyPolicyTests
{
    [Fact]
    public async Task CustomHeaderName_IsAccepted()
    {
        int executionCount = 0;

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost("/test", () =>
                    {
                        executionCount++;

                        return Results.Ok();
                    })
                    .RequireIdempotency();
                },
                configureAspNetOptions: options =>
                {
                    options.HeaderName = "X-Idempotency-Key";
                });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        firstRequest.Headers.Add(
            "X-Idempotency-Key",
            "test-key");

        using HttpResponseMessage firstResponse =
            await server.Client.SendAsync(firstRequest);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        secondRequest.Headers.Add(
            "X-Idempotency-Key",
            "test-key");

        using HttpResponseMessage secondResponse =
            await server.Client.SendAsync(secondRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task DefaultHeaderName_IsRejected_WhenCustomHeaderNameIsConfigured()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.Ok())
                        .RequireIdempotency();
                },
                configureAspNetOptions: options =>
                {
                    options.HeaderName = "X-Idempotency-Key";
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            "test-key");

        using HttpResponseMessage response =
            await server.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task KeyAtMaximumLength_IsAccepted()
    {
        const int maxKeyLength = 8;
        string key = new('a', maxKeyLength);

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.Ok())
                        .RequireIdempotency();
                },
                configureAspNetOptions: options =>
                {
                    options.MaxKeyLength = maxKeyLength;
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            key);

        using HttpResponseMessage response =
            await server.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task KeyOverMaximumLength_IsRejected()
    {
        const int maxKeyLength = 8;
        string key = new('a', maxKeyLength + 1);

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/test",
                            () => Results.Ok())
                        .RequireIdempotency();
                },
                configureAspNetOptions: options =>
                {
                    options.MaxKeyLength = maxKeyLength;
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            key);

        using HttpResponseMessage response =
            await server.Client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task EmptyHeaderName_FailsStartup()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(
            async () =>
            {
                await using IdempotencyTestServer server =
                    await IdempotencyTestServer.CreateAsync(
                        configureAspNetOptions: options =>
                        {
                            options.HeaderName = "";
                        });
            });
    }

    [Fact]
    public async Task NonPositiveMaxKeyLength_FailsStartup()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(
            async () =>
            {
                await using IdempotencyTestServer server =
                    await IdempotencyTestServer.CreateAsync(
                        configureAspNetOptions: options =>
                        {
                            options.MaxKeyLength = 0;
                        });
            });
    }
}