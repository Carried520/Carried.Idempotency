using System.Net;
using System.Text;
using Carried.Idempotency.AspNet.Extensions;
using Carried.Idempotency.AspNet.Tests.TestServer;
using Carried.Idempotency.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carried.Idempotency.AspNet.Tests.LeaseLoss;

public partial class IdempotencyIntegrationTests
{
    [Fact]
    public async Task LeaseLoss_CancelsEndpointCancellationToken()
    {
        var endpointStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var endpointCancelled =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var store =
            new LeaseLosingIdempotencyStore();

        var options =
            new IdempotencyOptions
            {
                LeaseDuration =
                    TimeSpan.FromMilliseconds(300),

                CompletedRetention =
                    TimeSpan.FromMinutes(1)
            };

        IdempotencyService service =
            IdempotencyService.Create(
                store,
                new TestIdempotencySerializer(),
                options);

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                endpoints =>
                {
                    endpoints.MapPost(
                            "/orders",
                            async (
                                CancellationToken cancellationToken) =>
                            {
                                endpointStarted.TrySetResult();

                                try
                                {
                                    await Task.Delay(
                                        Timeout.InfiniteTimeSpan,
                                        cancellationToken);
                                }
                                catch (OperationCanceledException)
                                    when (cancellationToken
                                              .IsCancellationRequested)
                                {
                                    endpointCancelled.TrySetResult();

                                    throw;
                                }

                                return Results.Ok();
                            })
                        .RequireIdempotency();
                },
                services =>
                {
                    services.RemoveAll<IdempotencyService>();

                    services.AddSingleton(service);
                });

        Task<HttpResponseMessage> request =
            BaseIntegration.IdempotencyIntegrationTests.SendAsync(
                server.Client,
                "/orders",
                "lease-loss-key",
                "{}");

        await endpointStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        await endpointCancelled.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        using HttpResponseMessage response =
            await request.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.True(
            store.RenewalAttempts >= 1);
    }


    [Fact]
    public async Task LeaseLoss_StopsCooperativeEndpointExecution()
    {
        var endpointStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int workCompleted = 0;

        var store =
            new LeaseLosingIdempotencyStore();

        var options =
            new IdempotencyOptions
            {
                LeaseDuration =
                    TimeSpan.FromMilliseconds(300),

                CompletedRetention =
                    TimeSpan.FromMinutes(1)
            };

        var service =
            IdempotencyService.Create(
                store,
                new TestIdempotencySerializer(),
                options);

        await using var server =
            await IdempotencyTestServer.CreateAsync(
                endpoints =>
                {
                    endpoints.MapPost(
                            "/orders",
                            async (
                                CancellationToken cancellationToken) =>
                            {
                                endpointStarted.TrySetResult();

                                await Task.Delay(
                                    Timeout.InfiniteTimeSpan,
                                    cancellationToken);

                                Interlocked.Increment(
                                    ref workCompleted);

                                return Results.Ok();
                            })
                        .RequireIdempotency();
                },
                services =>
                {
                    services.RemoveAll<IdempotencyService>();

                    services.AddSingleton(service);
                });

        Task<HttpResponseMessage> request =
            BaseIntegration.IdempotencyIntegrationTests.SendAsync(
                server.Client,
                "/orders",
                "lease-loss-key",
                "{}");

        await endpointStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        using HttpResponseMessage response =
            await request.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        Assert.Equal(
            0,
            Volatile.Read(ref workCompleted));

        Assert.True(
            store.RenewalAttempts >= 1);
    }


    [Fact]
    public async Task ClientCancellation_StillCancelsEndpointCancellationToken()
    {
        var endpointStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var endpointCancelled =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync(endpoints =>
            {
                endpoints.MapPost(
                        "/orders",
                        async (
                            CancellationToken cancellationToken) =>
                        {
                            endpointStarted.TrySetResult();

                            try
                            {
                                await Task.Delay(
                                    Timeout.InfiniteTimeSpan,
                                    cancellationToken);
                            }
                            catch (OperationCanceledException)
                                when (cancellationToken
                                          .IsCancellationRequested)
                            {
                                endpointCancelled.TrySetResult();

                                throw;
                            }

                            return Results.Ok();
                        })
                    .RequireIdempotency();
            });

        using var cancellation =
            new CancellationTokenSource();

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/orders");

        request.Headers.Add(
            "Idempotency-Key",
            "client-cancellation-key");

        request.Content =
            new StringContent(
                "{}",
                Encoding.UTF8,
                "application/json");

        Task<HttpResponseMessage> responseTask =
            server.Client.SendAsync(
                request,
                cancellation.Token);

        await endpointStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await endpointCancelled.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await responseTask; });
    }
}