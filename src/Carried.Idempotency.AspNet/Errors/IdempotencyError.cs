namespace Carried.Idempotency.AspNet.Errors;

public sealed record IdempotencyError(int StatusCode, string Code, string Description);