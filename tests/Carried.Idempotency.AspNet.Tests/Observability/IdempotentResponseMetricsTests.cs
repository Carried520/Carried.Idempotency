using System.Diagnostics.Metrics;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.AspNet.Policies;
using Carried.Idempotency.AspNet.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Carried.Idempotency.AspNet.Tests.Observability;

[Collection("IdempotencyMetrics")]
public sealed class IdempotentResponseMetricsTests
{
    [Fact]
    public async Task ExecuteAsync_RetainableResponse_RecordsRetained()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            new IdempotencyPolicy(),
            async httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status200OK;

                await httpContext.Response.WriteAsync("response");
            });

        MetricMeasurement measurement =
            Assert.Single(listener.Measurements, measurement =>
                measurement.InstrumentName ==
                IdempotencyMetrics.ResponsesInstrumentName);

        Assert.Equal(1, measurement.Value);
        Assert.Equal("retained", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ServerError_RecordsServerError()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            new IdempotencyPolicy(),
            async httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                await httpContext.Response.WriteAsync("error");
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("server_error", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_RequestTimeout_RecordsRequestTimeout()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            new IdempotencyPolicy(),
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status408RequestTimeout;

                return Task.CompletedTask;
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("request_timeout", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_RateLimited_RecordsRateLimited()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            new IdempotencyPolicy(),
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                return Task.CompletedTask;
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("rate_limited", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ClientErrorNotStored_RecordsClientErrorPolicy()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        var policy = new IdempotencyPolicy
        {
            StoreClientErrors = false
        };

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            policy,
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                return Task.CompletedTask;
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("client_error_policy", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ResponseTooLarge_RecordsResponseTooLarge()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        var policy = new IdempotencyPolicy
        {
            MaxRetainedResponseBodySize = 4
        };

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            policy,
            async httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status200OK;

                await httpContext.Response.WriteAsync("12345");
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("response_too_large", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_OversizedServerError_RecordsServerError()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        var policy = new IdempotencyPolicy
        {
            MaxRetainedResponseBodySize = 1
        };

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            policy,
            async httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                await httpContext.Response.WriteAsync("large-error-body");
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("server_error", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_StoredClientError_RecordsRetained()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        var policy = new IdempotencyPolicy
        {
            StoreClientErrors = true
        };

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            policy,
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status422UnprocessableEntity;

                return Task.CompletedTask;
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        Assert.Equal("retained", measurement.Result);

        await host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ResponseMeasurement_ContainsOnlyResultTag()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = CreateHostWithMetrics();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey(
                "secret-scope",
                "secret-client-key"),
            "secret-fingerprint",
            new IdempotencyPolicy(),
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status204NoContent;

                return Task.CompletedTask;
            });

        MetricMeasurement measurement =
            GetSingleResponseMeasurement(listener);

        KeyValuePair<string, object?> tag =
            Assert.Single(measurement.Tags);

        Assert.Equal("result", tag.Key);
        Assert.Equal("retained", tag.Value);

        Assert.DoesNotContain(
            measurement.Tags,
            pair =>
                Equals(pair.Value, "secret-scope") ||
                Equals(pair.Value, "secret-client-key") ||
                Equals(pair.Value, "secret-fingerprint"));

        await host.StopAsync();
    }

    [Fact]
    public async Task AddIdempotency_WithoutMetrics_DoesNotEmitResponseMetrics()
    {
        using var listener = new TestMeterListener();
        listener.Start();

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => { services.AddIdempotency(opts => opts.UseInMemory()); })
            .Build();

        await host.StartAsync();

        var executor =
            host.Services.GetRequiredService<IdempotentResponseExecutor>();

        var service =
            host.Services.GetRequiredService<IdempotencyService>();

        DefaultHttpContext context = CreateContext();

        await executor.ExecuteAsync(
            context,
            service,
            new IdempotencyKey("POST:/orders", "key-1"),
            "fingerprint",
            new IdempotencyPolicy(),
            httpContext =>
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status200OK;

                return Task.CompletedTask;
            });

        Assert.DoesNotContain(
            listener.Measurements,
            measurement =>
                measurement.InstrumentName ==
                IdempotencyMetrics.ResponsesInstrumentName);

        await host.StopAsync();
    }

    private static IHost CreateHostWithMetrics()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIdempotency(opts => opts.UseInMemory());
                services.AddIdempotencyMetrics();
            })
            .Build();
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            RequestAborted = CancellationToken.None
        };

        context.Response.Body = new MemoryStream();

        return context;
    }

    private static MetricMeasurement GetSingleResponseMeasurement(
        TestMeterListener listener)
    {
        return Assert.Single(listener.Measurements, measurement =>
            measurement.InstrumentName ==
            IdempotencyMetrics.ResponsesInstrumentName);
    }

    private sealed class TestMeterListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Lock _lock = new();
        private readonly List<MetricMeasurement> _measurements = [];

        public IReadOnlyList<MetricMeasurement> Measurements
        {
            get
            {
                lock (_lock)
                {
                    return _measurements.ToArray();
                }
            }
        }

        public TestMeterListener()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name ==
                    IdempotencyMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                lock (_lock)
                {
                    _measurements.Add(
                        new MetricMeasurement(
                            instrument.Name,
                            measurement,
                            tags.ToArray()));
                }
            });
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        long Value,
        KeyValuePair<string, object?>[] Tags)
    {
        public string? Result =>
            Tags
                .FirstOrDefault(tag => tag.Key == "result")
                .Value as string;
    }
}