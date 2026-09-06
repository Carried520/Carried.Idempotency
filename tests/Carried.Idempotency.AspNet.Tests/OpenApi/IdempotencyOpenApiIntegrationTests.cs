using System.Net;
using System.Text.Json;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Tests.TestServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Carried.Idempotency.AspNet.Tests.OpenApi;

public sealed class IdempotencyOpenApiIntegrationTests
{
    [Fact]
    public async Task MarkedMinimalApi_IncludesRequiredIdempotencyKeyHeader()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                opts => opts.UseInMemory(),
                endpoints =>
                {
                    endpoints
                        .MapPost(
                            "/openapi/orders",
                            () => Results.Ok())
                        .RequireIdempotency();
                },
                enableOpenApi: true);

        using JsonDocument document =
            await GetOpenApiDocumentAsync(server);

        JsonElement operation =
            GetOperation(
                document,
                "/openapi/orders",
                "post");

        AssertRequiredIdempotencyKeyHeader(operation);
    }

    [Fact]
    public async Task UnmarkedMinimalApi_DoesNotIncludeIdempotencyKeyHeader()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(
                opts => opts.UseInMemory(),
                endpoints =>
                {
                    endpoints.MapPost(
                        "/openapi/orders",
                        () => Results.Ok());
                },
                enableOpenApi: true);

        using JsonDocument document =
            await GetOpenApiDocumentAsync(server);

        JsonElement operation =
            GetOperation(
                document,
                "/openapi/orders",
                "post");

        AssertNoIdempotencyKeyHeader(operation);
    }

    [Fact]
    public async Task MarkedControllerAction_IncludesRequiredIdempotencyKeyHeader()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(opts => opts.UseInMemory(), enableOpenApi: true);

        using JsonDocument document =
            await GetOpenApiDocumentAsync(server);

        JsonElement operation =
            GetOperation(
                document,
                "/openapi-test/action",
                "post");

        AssertRequiredIdempotencyKeyHeader(operation);
    }

    [Fact]
    public async Task ClassLevelMarkedController_IncludesRequiredIdempotencyKeyHeader()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(opts => opts.UseInMemory(), enableOpenApi: true);

        using JsonDocument document =
            await GetOpenApiDocumentAsync(server);

        JsonElement operation =
            GetOperation(
                document,
                "/openapi-test/class",
                "post");

        AssertRequiredIdempotencyKeyHeader(operation);
    }

    [Fact]
    public async Task UnmarkedControllerAction_DoesNotIncludeIdempotencyKeyHeader()
    {
        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(opts => opts.UseInMemory(), enableOpenApi: true);

        using JsonDocument document =
            await GetOpenApiDocumentAsync(server);

        JsonElement operation =
            GetOperation(
                document,
                "/openapi-test/unmarked",
                "post");

        AssertNoIdempotencyKeyHeader(operation);
    }

    private static async Task<JsonDocument> GetOpenApiDocumentAsync(
        IdempotencyTestServer server)
    {
        using HttpResponseMessage response =
            await server.Client.GetAsync("/openapi/v1.json");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Stream stream =
            await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement GetOperation(
        JsonDocument document,
        string path,
        string method)
    {
        return document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method);
    }

    private static void AssertRequiredIdempotencyKeyHeader(
        JsonElement operation)
    {
        Assert.True(
            operation.TryGetProperty(
                "parameters",
                out JsonElement parameters));

        JsonElement? header =
            FindIdempotencyKeyHeader(parameters);

        Assert.True(header.HasValue);

        Assert.Equal(
            "Idempotency-Key",
            header.Value
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            "header",
            header.Value
                .GetProperty("in")
                .GetString());

        Assert.True(
            header.Value
                .GetProperty("required")
                .GetBoolean());

        Assert.Equal(
            "string",
            header.Value
                .GetProperty("schema")
                .GetProperty("type")
                .GetString());
    }

    private static void AssertNoIdempotencyKeyHeader(
        JsonElement operation)
    {
        if (!operation.TryGetProperty(
                "parameters",
                out JsonElement parameters))
        {
            return;
        }

        Assert.Null(FindIdempotencyKeyHeader(parameters));
    }

    private static JsonElement? FindIdempotencyKeyHeader(
        JsonElement parameters)
    {
        foreach (JsonElement parameter
                 in parameters.EnumerateArray())
        {
            if (!parameter.TryGetProperty(
                    "name",
                    out JsonElement name) ||
                !parameter.TryGetProperty(
                    "in",
                    out JsonElement location))
            {
                continue;
            }

            if (string.Equals(
                    name.GetString(),
                    "Idempotency-Key",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    location.GetString(),
                    "header",
                    StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }
}