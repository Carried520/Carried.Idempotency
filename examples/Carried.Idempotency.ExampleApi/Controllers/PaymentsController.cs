using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Carried.Idempotency.ExampleApi.Controllers;

[ApiController]
public class PaymentsController : ControllerBase
{
    [HttpPost("/payments")]
    [RequireIdempotency]
    public IActionResult GetPayments()
    {
        return Ok(new { Id = 123 });
    }

    [HttpPost("/fake-payments")]
    [RequireIdempotency]
    public IActionResult GetFakePayments()
    {
        return Ok(new { Id = 123 });
    }
}