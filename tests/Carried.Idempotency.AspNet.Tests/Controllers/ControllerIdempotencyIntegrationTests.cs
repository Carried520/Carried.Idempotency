using System.Net;
using System.Text;
using Carried.Idempotency.AspNet.Tests.TestServer;

namespace Carried.Idempotency.AspNet.Tests.Controllers;

public sealed class ControllerIdempotencyIntegrationTests
{
    [Fact]
    public async Task ControllerAction_WithAttribute_RequiresIdempotencyKey()
    {
        TestOrdersController.Reset();

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync();

        using HttpResponseMessage response =
            await server.Client.PostAsync(
                "/test-controller/orders",
                content: null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            0,
            TestOrdersController.MethodInvocationCount);
    }

    [Fact]
    public async Task ControllerAction_WithAttribute_ReplaysSameRequest()
    {
        TestOrdersController.Reset();

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync();

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/test-controller/orders",
                "controller-key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/test-controller/orders",
                "controller-key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.Created,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            second.StatusCode);

        Assert.Equal(
            1,
            TestOrdersController.MethodInvocationCount);

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
    public async Task ControllerAction_WithAttribute_DifferentRequest_ReturnsConflict()
    {
        TestOrdersController.Reset();

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync();

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/test-controller/orders",
                "controller-key-1",
                """
                {
                    "value": 1
                }
                """);

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/test-controller/orders",
                "controller-key-1",
                """
                {
                    "value": 2
                }
                """);

        Assert.Equal(
            HttpStatusCode.Created,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(
            1,
            TestOrdersController.MethodInvocationCount);
    }

    [Fact]
    public async Task Controller_WithClassLevelAttribute_RequiresIdempotencyKey()
    {
        TestPaymentsController.Reset();

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync();

        using HttpResponseMessage response =
            await server.Client.PostAsync(
                "/test-controller/payments",
                content: null);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            0,
            TestPaymentsController.InvocationCount);
    }

    [Fact]
    public async Task Controller_WithClassLevelAttribute_ReplaysSameRequest()
    {
        TestPaymentsController.Reset();

        await using IdempotencyTestServer server =
            await IdempotencyTestServer.CreateAsync();

        using HttpResponseMessage first =
            await SendAsync(
                server.Client,
                "/test-controller/payments",
                "payments-key-1",
                "{}");

        using HttpResponseMessage second =
            await SendAsync(
                server.Client,
                "/test-controller/payments",
                "payments-key-1",
                "{}");

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            second.StatusCode);

        Assert.Equal(
            1,
            TestPaymentsController.InvocationCount);
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