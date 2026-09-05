using System.Net;
using Carried.Idempotency.AspNet.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyReplayHeadersTests
{
    [Fact]
    public async Task CustomReplayHeader_IsReplayed()
    {
        var executionCount = 0;

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost("/test", (HttpContext context) =>
                        {
                            executionCount++;

                            context.Response.Headers["X-Resource-Version"] = "v1";

                            return Results.Ok();
                        })
                        .RequireIdempotency();
                },
                configureAspNetOptions: options => { options.ReplayHeaders.Add("X-Resource-Version"); });

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

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(1, executionCount);

        Assert.True(
            secondResponse.Headers.TryGetValues(
                "X-Resource-Version",
                out IEnumerable<string>? values));

        Assert.Equal(
            "v1",
            Assert.Single(values));
    }

    [Fact]
    public async Task RemovedDefaultReplayHeader_IsNotReplayed()
    {
        var executionCount = 0;

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost("/test", (HttpContext context) =>
                        {
                            executionCount++;

                            context.Response.Headers.Location =
                                "/resource/123";

                            return Results.Ok();
                        })
                        .RequireIdempotency();
                },
                configureAspNetOptions: options => { options.ReplayHeaders.Remove("Location"); });

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

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(1, executionCount);

        Assert.Null(secondResponse.Headers.Location);
    }
}