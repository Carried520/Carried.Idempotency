using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Observability;
using Carried.Idempotency.Exceptions;
using Carried.Idempotency.IdempotencyOperation;
using Carried.Idempotency.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carried.Idempotency.AspNet.Tests;

public sealed class IdempotencyEventLoggerTests
{
    [Fact]
    public async Task StartAsync_CompletedOperation_LogsAcquiredAndCompleted()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        var logger = new TestLogger<IdempotencyEventLogger>();
        var eventLogger = new IdempotencyEventLogger(service, logger);

        await eventLogger.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Collection(
            logger.Entries,
            acquired =>
            {
                Assert.Equal(LogLevel.Debug, acquired.Level);
                Assert.Equal(1, acquired.EventId.Id);
                Assert.Contains(
                    "Idempotency execution acquired",
                    acquired.Message);
                Assert.Contains(
                    "POST:/orders",
                    acquired.Message);
            },
            completed =>
            {
                Assert.Equal(LogLevel.Debug, completed.Level);
                Assert.Equal(5, completed.EventId.Id);
                Assert.Contains(
                    "Idempotency execution completed",
                    completed.Message);
                Assert.Contains(
                    "POST:/orders",
                    completed.Message);
            });
    }

    [Fact]
    public async Task StartAsync_CompletedEntry_LogsReplayed()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        var logger = new TestLogger<IdempotencyEventLogger>();
        var eventLogger = new IdempotencyEventLogger(service, logger);

        await eventLogger.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        logger.Entries.Clear();

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("other")));

        TestLogEntry replayed = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, replayed.Level);
        Assert.Equal(3, replayed.EventId.Id);
        Assert.Contains(
            "Idempotency result replayed",
            replayed.Message);
        Assert.Contains(
            "POST:/orders",
            replayed.Message);
    }

    [Fact]
    public async Task StartAsync_Conflict_LogsWarning()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        var logger = new TestLogger<IdempotencyEventLogger>();
        var eventLogger = new IdempotencyEventLogger(service, logger);

        await eventLogger.StartAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint-1",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        logger.Entries.Clear();

        await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => service.ExecuteAsync(
                key,
                "fingerprint-2",
                _ => Task.FromResult(
                    IdempotencyOperationResult<string>.Complete("other"))));

        TestLogEntry conflict = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, conflict.Level);
        Assert.Equal(4, conflict.EventId.Id);
        Assert.Contains(
            "Idempotency conflict detected",
            conflict.Message);
        Assert.Contains(
            "POST:/orders",
            conflict.Message);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLifecycleEvents()
    {
        IdempotencyService service = IdempotencyService.CreateInMemory(
            new IdempotencyOptions());

        var logger = new TestLogger<IdempotencyEventLogger>();
        var eventLogger = new IdempotencyEventLogger(service, logger);

        await eventLogger.StartAsync(CancellationToken.None);
        await eventLogger.StopAsync(CancellationToken.None);

        var key = new IdempotencyKey(
            "POST:/orders",
            "key-1");

        await service.ExecuteAsync(
            key,
            "fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AddIdempotency_HostStarted_ActivatesLifecycleLogging()
    {
        var loggerProvider = new TestLoggerProvider();

        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(loggerProvider);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
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
            "secret-client-key");

        await service.ExecuteAsync(
            key,
            "secret-fingerprint",
            _ => Task.FromResult(
                IdempotencyOperationResult<string>.Complete("result")));

        await host.StopAsync();

        Assert.Contains(
            loggerProvider.Entries,
            entry =>
                entry.EventId.Id == 1 &&
                entry.Level == LogLevel.Debug &&
                entry.Message.Contains("POST:/orders"));

        Assert.Contains(
            loggerProvider.Entries,
            entry =>
                entry.EventId.Id == 5 &&
                entry.Level == LogLevel.Debug &&
                entry.Message.Contains("POST:/orders"));

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry =>
                entry.Message.Contains("secret-client-key"));

        Assert.DoesNotContain(
            loggerProvider.Entries,
            entry =>
                entry.Message.Contains("secret-fingerprint"));
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<TestLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(
                new TestLogEntry(
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    exception));
        }
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        public List<TestLogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new ProviderLogger(Entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class ProviderLogger(
        List<TestLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(
                new TestLogEntry(
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    exception));
        }
    }

    private sealed record TestLogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception);
}