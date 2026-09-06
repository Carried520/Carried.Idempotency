using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet.Fingerprinting;

internal sealed class HeaderFingerprintContributor : IIdempotencyFingerprintContributor
{
    private readonly string _headerName;

    public string Name { get; }

    public HeaderFingerprintContributor(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        _headerName = headerName;
        Name = $"header:{headerName}";
    }

    public ValueTask<string?> GetValueAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Headers.TryGetValue(_headerName, out StringValues values))
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult<string?>(values.ToString());
    }
}