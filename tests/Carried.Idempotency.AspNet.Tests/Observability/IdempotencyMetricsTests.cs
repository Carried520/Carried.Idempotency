using System.Diagnostics.Metrics;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.Exceptions;
using Carried.Idempotency.IdempotencyOperation;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Carried.Idempotency.AspNet.Tests.Observability;

public sealed class IdempotencyMetricsTests
{
    [Fact]
    public async Task StartAsync_CompletedOperation_RecordsAcquiredAndCompleted()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "secret-client-key");

        await service.ExecuteAsync(
            key,
            "secret-fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Collection(
            listener.Measurements,
            acquired =>
            {
                Assert.Equal(
                    IdempotencyMetrics.OperationsInstrumentName,
                    acquired.InstrumentName);

                Assert.Equal(1, acquired.Value);
                Assert.Equal("acquired", acquired.Outcome);
            },
            completed =>
            {
                Assert.Equal(
                    IdempotencyMetrics.OperationsInstrumentName,
                    completed.InstrumentName);

                Assert.Equal(1, completed.Value);
                Assert.Equal("completed", completed.Outcome);
            });
    }

    [Fact]
    public async Task StartAsync_CompletedEntry_RecordsReplayed()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        listener.Clear();

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("other")));

        MetricMeasurement replayed =
            Assert.Single(listener.Measurements);

        Assert.Equal(
            IdempotencyMetrics.OperationsInstrumentName,
            replayed.InstrumentName);

        Assert.Equal(1, replayed.Value);
        Assert.Equal("replayed", replayed.Outcome);
    }

    [Fact]
    public async Task StartAsync_Conflict_RecordsConflict()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint-1",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        listener.Clear();

        await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => service.ExecuteAsync(
                key,
                "fingerprint-2",
                _ => Task.FromResult(
                    IdempotencyOperationResult<string>.Complete("other"))));

        MetricMeasurement conflict =
            Assert.Single(listener.Measurements);

        Assert.Equal(
            IdempotencyMetrics.OperationsInstrumentName,
            conflict.InstrumentName);

        Assert.Equal(1, conflict.Value);
        Assert.Equal("conflict", conflict.Outcome);
    }

    [Fact]
    public async Task StartAsync_ReleasedOperation_RecordsAcquiredAndReleased()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Release("result")));

        Assert.Collection(
            listener.Measurements,
            acquired =>
            {
                Assert.Equal(
                    IdempotencyMetrics.OperationsInstrumentName,
                    acquired.InstrumentName);

                Assert.Equal(1, acquired.Value);
                Assert.Equal("acquired", acquired.Outcome);
            },
            released =>
            {
                Assert.Equal(
                    IdempotencyMetrics.OperationsInstrumentName,
                    released.InstrumentName);

                Assert.Equal(1, released.Value);
                Assert.Equal("released", released.Outcome);
            });
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLifecycleEvents()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);
        await metrics.StopAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Empty(listener.Measurements);
    }

    [Fact]
    public async Task Measurements_ContainOnlyOutcomeTag()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        using var metrics = new IdempotencyMetrics(service);
        using var listener = new TestMeterListener();

        listener.Start();

        await metrics.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "secret-scope",
            "secret-client-key");

        await service.ExecuteAsync(
            key,
            "secret-fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.NotEmpty(listener.Measurements);

        foreach (MetricMeasurement measurement in listener.Measurements)
        {
            KeyValuePair<string, object?> tag =
                Assert.Single(measurement.Tags);

            Assert.Equal("outcome", tag.Key);

            Assert.DoesNotContain(
                measurement.Tags,
                pair =>
                    Equals(pair.Value, "secret-scope") ||
                    Equals(pair.Value, "secret-client-key") ||
                    Equals(pair.Value, "secret-fingerprint"));
        }
    }

    [Fact]
    public async Task AddIdempotencyMetrics_HostStarted_ActivatesLifecycleMetrics()
    {
        using var listener = new TestMeterListener();

        listener.Start();

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIdempotency();
                services.AddIdempotencyMetrics();
            })
            .Build();

        await host.StartAsync();

        IdempotencyService service =
            host.Services.GetRequiredService<IdempotencyService>();

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Contains(
            listener.Measurements,
            measurement =>
                measurement.InstrumentName ==
                IdempotencyMetrics.OperationsInstrumentName &&
                measurement.Outcome == "acquired");

        Assert.Contains(
            listener.Measurements,
            measurement =>
                measurement.InstrumentName ==
                IdempotencyMetrics.OperationsInstrumentName &&
                measurement.Outcome == "completed");

        await host.StopAsync();
    }

    [Fact]
    public async Task AddIdempotency_WithoutMetrics_DoesNotEmitLifecycleMetrics()
    {
        using var listener = new TestMeterListener();

        listener.Start();

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddIdempotency();
            })
            .Build();

        await host.StartAsync();

        IdempotencyService service =
            host.Services.GetRequiredService<IdempotencyService>();

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Empty(listener.Measurements);

        await host.StopAsync();
    }

    private sealed class TestMeterListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly object _lock = new();
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
                if (instrument.Meter.Name == IdempotencyMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>(
                (instrument, measurement, tags, state) =>
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

        public void Clear()
        {
            lock (_lock)
            {
                _measurements.Clear();
            }
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
        public string? Outcome =>
            Tags
                .FirstOrDefault(tag => tag.Key == "outcome")
                .Value as string;
    }
}