using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyIntegrationTests
{
    [Fact]
    public async Task UnmarkedEndpoint_DoesNotRequireIdempotencyKey()
    {
        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                Results.Ok(new { Id = 1 }));
        });

        HttpResponseMessage response =
            await server.Client.PostAsync(
                "/orders",
                content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task MarkedEndpoint_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                    Results.Ok())
                .RequireIdempotency();
        });

        HttpResponseMessage response =
            await server.Client.PostAsync(
                "/orders",
                content: null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task SameKeyAndSameRequest_ReplaysResponse()
    {
        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                {
                    invocationCount++;

                    return Results.Created(
                        "/orders/123",
                        new
                        {
                            Id = 123
                        });
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                """
                {
                    "productId": 42
                }
                """);

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                """
                {
                    "productId": 42
                }
                """);

        Assert.Equal(1, invocationCount);

        Assert.Equal(
            first.StatusCode,
            second.StatusCode);

        string firstBody =
            await first.Content.ReadAsStringAsync();

        string secondBody =
            await second.Content.ReadAsStringAsync();

        Assert.Equal(
            firstBody,
            secondBody);

        Assert.Equal(
            first.Headers.Location,
            second.Headers.Location);
    }

    [Fact]
    public async Task SameKeyAndDifferentBody_ReturnsConflict()
    {
        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                {
                    invocationCount++;

                    return Results.Ok();
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                """
                {
                    "productId": 42
                }
                """);

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                """
                {
                    "productId": 99
                }
                """);

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task SameKeyAndDifferentQuery_ReturnsConflict()
    {
        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                {
                    invocationCount++;

                    return Results.Ok();
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders?currency=EUR",
                "key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders?currency=USD",
                "key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task SameKeyAndDifferentRouteValue_ReturnsConflict()
    {
        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders/{id:int}", (int id) =>
                {
                    invocationCount++;

                    return Results.Ok(new
                    {
                        Id = id
                    });
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders/1",
                "key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders/2",
                "key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task SameKeyWhileFirstRequestIsRunning_ReturnsInProgress()
    {
        var enteredEndpoint =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseEndpoint =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", async () =>
                {
                    Interlocked.Increment(
                        ref invocationCount);

                    enteredEndpoint.SetResult();

                    await releaseEndpoint.Task;

                    return Results.Ok();
                })
                .RequireIdempotency();
        });

        Task<HttpResponseMessage> firstRequest =
            SendAsync(
                server.Client,
                "/orders",
                "key-1",
                "{}");

        await enteredEndpoint.Task;

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(1, invocationCount);

        releaseEndpoint.SetResult();

        using HttpResponseMessage first =
            await firstRequest;

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);
    }
    
    
    
    [Fact]
    public async Task ReplayedResponse_PreservesAllowedHeaders()
    {
        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", (HttpContext context) =>
                {
                    context.Response.Headers.ETag = "\"abc\"";
                    context.Response.Headers.CacheControl = "no-cache";

                    return Results.Created(
                        "/orders/123",
                        new { Id = 123 });
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders",
                "key-1",
                "{}");

        Assert.Equal(
            first.Headers.ETag,
            second.Headers.ETag);

        Assert.Equal(
            first.Headers.CacheControl?.ToString(),
            second.Headers.CacheControl?.ToString());

        Assert.Equal(
            first.Headers.Location,
            second.Headers.Location);
    }
    
    
    [Fact]
    public async Task QueryParameterOrder_DoesNotChangeFingerprint()
    {
        var invocationCount = 0;

        var server = await IdempotencyTestServer.CreateAsync(endpoints =>
        {
            endpoints.MapPost("/orders", () =>
                {
                    invocationCount++;
                    return Results.Ok();
                })
                .RequireIdempotency();
        });

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/orders?a=1&b=2",
                "key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/orders?b=2&a=1",
                "key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.OK,
            second.StatusCode);

        Assert.Equal(1, invocationCount);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string uri,
        string idempotencyKey,
        string body)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                uri);

        request.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        request.Content =
            new StringContent(
                body,
                Encoding.UTF8,
                "application/json");

        return await client.SendAsync(request);
    }
}