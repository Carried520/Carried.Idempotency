using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet.Fingerprinting;

internal static class RequestFingerprintProvider
{
    private const byte AbsentMarker = 0;
    private const byte PresentMarker = 1;

    public static async Task<string> CreateAsync(
        HttpContext context,
        string? routePattern,
        IEnumerable<IIdempotencyFingerprintContributor> contributors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(contributors);

        if (string.IsNullOrWhiteSpace(routePattern))
        {
            throw new InvalidOperationException("Idempotency requires a route pattern.");
        }

        using var hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Append(hash, context.Request.Method);
        Append(hash, routePattern);

        foreach (KeyValuePair<string, object?> routeValue in
                 context.Request.RouteValues.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            Append(hash, routeValue.Key);
            Append(
                hash,
                routeValue.Value?.ToString() ?? string.Empty);
        }

        foreach (KeyValuePair<string, StringValues> queryParameter in
                 context.Request.Query.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            Append(hash, queryParameter.Key);

            foreach (string? value in queryParameter.Value)
            {
                Append(hash, value ?? string.Empty);
            }
        }

        Append(
            hash,
            context.Request.ContentType ?? string.Empty);

        context.Request.EnableBuffering();

        var buffer = new byte[8192];

        try
        {
            int bytesRead;

            while ((bytesRead =
                       await context.Request.Body.ReadAsync(
                           buffer,
                           cancellationToken)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, bytesRead));
            }
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        foreach (IIdempotencyFingerprintContributor contributor in contributors.OrderBy(
                     x => x.Name,
                     StringComparer.Ordinal))
        {
            string? contributorValue = await contributor.GetValueAsync(context, cancellationToken);
            Append(hash, contributor.Name);

            if (contributorValue is null)
            {
                hash.AppendData([AbsentMarker]);
            }
            else
            {
                hash.AppendData([PresentMarker]);
                Append(hash, contributorValue);
            }
        }

        byte[] fingerprintBytes =
            hash.GetHashAndReset();

        return Convert.ToHexString(fingerprintBytes);
    }

    // Prefix each string with its byte length so distinct sequences
    // cannot produce the same canonical byte stream by concatenation.
    private static void Append(
        IncrementalHash hash,
        string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        Span<byte> lengthBytes =
            stackalloc byte[sizeof(int)];

        BinaryPrimitives.WriteInt32BigEndian(
            lengthBytes,
            bytes.Length);

        hash.AppendData(lengthBytes);
        hash.AppendData(bytes);
    }
}