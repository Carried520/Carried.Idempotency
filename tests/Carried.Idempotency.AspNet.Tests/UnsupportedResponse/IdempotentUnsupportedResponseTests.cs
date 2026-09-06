using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Carried.Idempotency.AspNet.Tests.UnsupportedResponse;

public sealed class IdempotentUnsupportedResponseTests
{
    [Fact]
    public async Task EventStreamResponse_ReturnsUnsupportedResponseError()
    {
        await using WebApplication app =
            await CreateAppAsync(async context =>
            {
                context.Response.ContentType =
                    "text/event-stream";

                await context.Response.WriteAsync(
                    "data: hello\n\n");
            });

        HttpClient client = app.GetTestClient();

        using var request =
            CreateRequest("test-key");

        using HttpResponseMessage response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        string body =
            await response.Content.ReadAsStringAsync();

        using JsonDocument document =
            JsonDocument.Parse(body);

        Assert.Equal(
            "idempotency_unsupported_response",
            document.RootElement
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task EventStreamResponse_WithParameters_ReturnsUnsupportedResponseError()
    {
        await using WebApplication app =
            await CreateAppAsync(async context =>
            {
                context.Response.ContentType =
                    "text/event-stream; charset=utf-8";

                await context.Response.WriteAsync(
                    "data: hello\n\n");
            });

        HttpClient client = app.GetTestClient();

        using var request =
            CreateRequest("test-key");

        using HttpResponseMessage response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        string body =
            await response.Content.ReadAsStringAsync();

        using JsonDocument document =
            JsonDocument.Parse(body);

        Assert.Equal(
            "idempotency_unsupported_response",
            document.RootElement
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task EventStreamResponse_IsReleased()
    {
        int executions = 0;

        await using WebApplication app =
            await CreateAppAsync(async context =>
            {
                executions++;

                context.Response.ContentType =
                    "text/event-stream";

                await context.Response.WriteAsync(
                    "data: hello\n\n");
            });

        HttpClient client = app.GetTestClient();

        using var firstRequest =
            CreateRequest("same-key");

        using HttpResponseMessage firstResponse =
            await client.SendAsync(firstRequest);

        using var secondRequest =
            CreateRequest("same-key");

        using HttpResponseMessage secondResponse =
            await client.SendAsync(secondRequest);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            secondResponse.StatusCode);

        Assert.Equal(
            2,
            executions);
    }

    [Fact]
    public async Task EventStreamResponse_RecordsUnsupportedResponseMetric()
    {
        var results = new List<string>();

        using var listener =
            new MeterListener();

        listener.InstrumentPublished =
            (instrument, meterListener) =>
            {
                if (instrument.Meter.Name ==
                        IdempotencyMetrics.MeterName &&
                    instrument.Name ==
                        IdempotencyMetrics.ResponsesInstrumentName)
                {
                    meterListener.EnableMeasurementEvents(
                        instrument);
                }
            };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "result" &&
                        tag.Value is string result)
                    {
                        results.Add(result);
                    }
                }
            });

        listener.Start();

        await using WebApplication app =
            await CreateAppAsync(
                async context =>
                {
                    context.Response.ContentType =
                        "text/event-stream";

                    await context.Response.WriteAsync(
                        "data: hello\n\n");
                },
                addMetrics: true);

        HttpClient client = app.GetTestClient();

        using var request =
            CreateRequest("metric-key");

        using HttpResponseMessage response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.Contains(
            IdempotencyMetricResults.UnsupportedResponse,
            results);
    }
    
    [Fact]
    public async Task EventStreamResponse_IsRejectedBeforeEndpointCanContinueStreaming()
    {
        var afterFirstWriteReached =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await using WebApplication app =
            await CreateAppAsync(async context =>
            {
                context.Response.ContentType =
                    "text/event-stream";

                await context.Response.WriteAsync(
                    "data: hello\n\n");

                afterFirstWriteReached.TrySetResult();

                await Task.Delay(
                    Timeout.Infinite,
                    context.RequestAborted);
            });

        HttpClient client =
            app.GetTestClient();

        using var request =
            CreateRequest("streaming-key");

        using HttpResponseMessage response =
            await client.SendAsync(request)
                .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.False(
            afterFirstWriteReached.Task.IsCompleted);
    }

    private static async Task<WebApplication> CreateAppAsync(
        RequestDelegate endpoint,
        bool addMetrics = false)
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddIdempotency(opts => opts.UseInMemory());

        if (addMetrics)
        {
            builder.Services.AddIdempotencyMetrics();
        }

        WebApplication app =
            builder.Build();

        app.UseIdempotency();

        app.MapPost(
                "/test",
                endpoint)
            .RequireIdempotency();

        await app.StartAsync();

        return app;
    }

    private static HttpRequestMessage CreateRequest(
        string key)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/test");

        request.Headers.Add(
            "Idempotency-Key",
            key);

        return request;
    }
}