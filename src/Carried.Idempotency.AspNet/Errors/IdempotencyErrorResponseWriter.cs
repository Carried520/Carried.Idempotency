using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Carried.Idempotency.AspNet.Errors;

internal sealed class IdempotencyErrorResponseWriter : IIdempotencyErrorResponseWriter
{
    private readonly IProblemDetailsService _problemDetailsService;

    public IdempotencyErrorResponseWriter(IProblemDetailsService problemDetailsService)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        _problemDetailsService = problemDetailsService;
    }

    public async Task WriteAsync(
        HttpContext context,
        IdempotencyError error,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = error.StatusCode;

        var problemDetails = new ProblemDetails
        {
            Status = error.StatusCode,
            Title = "Idempotency request failed.",
            Detail = error.Description,
            Extensions =
            {
                ["code"] = error.Code
            }
        };

        await _problemDetailsService.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problemDetails });
    }
}