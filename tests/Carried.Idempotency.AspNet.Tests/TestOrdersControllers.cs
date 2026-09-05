using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Carried.Idempotency.AspNet.Tests;

[ApiController]
[Route("test-controller/orders")]
public sealed class TestOrdersController : ControllerBase
{
    private static int _methodInvocationCount;

    public static int MethodInvocationCount =>
        Volatile.Read(ref _methodInvocationCount);

    public static void Reset()
    {
        Interlocked.Exchange(
            ref _methodInvocationCount,
            0);
    }

    [HttpPost]
    [RequireIdempotency]
    public IActionResult Create()
    {
        Interlocked.Increment(
            ref _methodInvocationCount);

        return Created(
            "/test-controller/orders/123",
            new
            {
                Id = 123
            });
    }
}