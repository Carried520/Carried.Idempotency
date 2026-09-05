using Carried.Idempotency.AspNet.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Carried.Idempotency.AspNet.Tests.Controllers;

[ApiController]
[Route("openapi-test")]
public sealed class OpenApiTestController :
    ControllerBase
{
    [HttpPost("action")]
    [RequireIdempotency]
    public IActionResult MarkedAction()
    {
        return Ok();
    }

    [HttpPost("unmarked")]
    public IActionResult UnmarkedAction()
    {
        return Ok();
    }
}

[ApiController]
[Route("openapi-test/class")]
[RequireIdempotency]
public sealed class OpenApiClassLevelTestController :
    ControllerBase
{
    [HttpPost]
    public IActionResult Create()
    {
        return Ok();
    }
}