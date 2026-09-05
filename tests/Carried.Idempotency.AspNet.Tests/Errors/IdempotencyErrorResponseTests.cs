using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carried.Idempotency.AspNet.Errors;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Tests.TestServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyErrorResponseTests
{
    [Fact]
    public async Task MissingIdempotencyKey_WritesProblemDetails()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/orders",
                            () => Results.Ok())
                        .RequireIdempotency();
                });

        HttpResponseMessage response =
            await server.Client.PostAsync(
                "/orders",
                null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetails? problemDetails =
            await response.Content
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problemDetails.Status);

        Assert.Equal(
            "Idempotency request failed.",
            problemDetails.Title);

        Assert.Contains(
            "Idempotency-Key",
            problemDetails.Detail);

        Assert.True(
            problemDetails.Extensions.TryGetValue(
                "code",
                out object? code));

        Assert.Equal(
            "invalid_idempotency_key",
            GetExtensionString(code));
    }

    [Fact]
    public async Task Conflict_WritesProblemDetails()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/orders",
                            async (HttpContext context) =>
                            {
                                using var reader =
                                    new StreamReader(
                                        context.Request.Body);

                                string body =
                                    await reader.ReadToEndAsync();

                                return Results.Ok(body);
                            })
                        .RequireIdempotency();
                });

        using var firstRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/orders");

        firstRequest.Headers.Add(
            "Idempotency-Key",
            "order-1");

        firstRequest.Content =
            JsonContent.Create(
                new
                {
                    value = 1
                });

        HttpResponseMessage firstResponse =
            await server.Client.SendAsync(
                firstRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        using var secondRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/orders");

        secondRequest.Headers.Add(
            "Idempotency-Key",
            "order-1");

        secondRequest.Content =
            JsonContent.Create(
                new
                {
                    value = 2
                });

        HttpResponseMessage secondResponse =
            await server.Client.SendAsync(
                secondRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);

        ProblemDetails? problemDetails =
            await secondResponse.Content
                .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);

        Assert.Equal(
            StatusCodes.Status409Conflict,
            problemDetails.Status);

        Assert.Equal(
            "Idempotency request failed.",
            problemDetails.Title);

        Assert.NotNull(
            problemDetails.Detail);

        Assert.True(
            problemDetails.Extensions.TryGetValue(
                "code",
                out object? code));

        Assert.Equal(
            "idempotency_conflict",
            GetExtensionString(code));
    }

    [Fact]
    public async Task CustomWriter_OverridesProblemDetailsWriter()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                configureEndpoints: endpoints =>
                {
                    endpoints.MapPost(
                            "/orders",
                            () => Results.Ok())
                        .RequireIdempotency();
                },
                configureServices: services =>
                {
                    services.AddSingleton<
                        IIdempotencyErrorResponseWriter,
                        TestErrorResponseWriter>();
                });

        HttpResponseMessage response =
            await server.Client.PostAsync(
                "/orders",
                null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        TestErrorResponse? error =
            await response.Content
                .ReadFromJsonAsync<TestErrorResponse>();

        Assert.NotNull(error);

        Assert.Equal(
            "custom_error",
            error.Error);

        Assert.Equal(
            "invalid_idempotency_key",
            error.OriginalCode);
    }

    private static string? GetExtensionString(
        object? value)
    {
        return value switch
        {
            string stringValue =>
                stringValue,

            JsonElement
            {
                ValueKind: JsonValueKind.String
            } element =>
                element.GetString(),

            _ =>
                null
        };
    }

    private sealed class TestErrorResponseWriter
        : IIdempotencyErrorResponseWriter
    {
        public async Task WriteAsync(
            HttpContext context,
            IdempotencyError error,
            CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode =
                error.StatusCode;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = "custom_error",
                    originalCode = error.Code
                },
                cancellationToken);
        }
    }

    private sealed record TestErrorResponse(
        string Error,
        string OriginalCode);
}