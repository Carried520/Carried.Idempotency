using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Carried.Idempotency.AspNet.Tests;

[ApiController]
[Route("test-controller/payments")]
[RequireIdempotency]
public sealed class TestPaymentsController : ControllerBase
{
    private static int _invocationCount;

    public static int InvocationCount =>
        Volatile.Read(ref _invocationCount);

    public static void Reset()
    {
        Interlocked.Exchange(
            ref _invocationCount,
            0);
    }

    [HttpPost]
    public IActionResult Create()
    {
        Interlocked.Increment(
            ref _invocationCount);

        return Ok(new
        {
            Id = 456
        });
    }
}