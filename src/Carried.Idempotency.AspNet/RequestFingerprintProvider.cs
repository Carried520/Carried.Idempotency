using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Carried.Idempotency.AspNet;

public sealed class RequestFingerprintProvider
{
    public async Task<string> CreateAsync(
        HttpContext context,
       string? routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            throw new InvalidOperationException(
                "Idempotency requires a route pattern.");
        }

        using var hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Append(hash, context.Request.Method);
        Append(hash, routePattern);

        foreach (KeyValuePair<string, object?> routeValue in
                 context.Request.RouteValues.OrderBy(x => x.Key))
        {
            Append(hash, routeValue.Key);
            Append(
                hash,
                routeValue.Value?.ToString() ?? string.Empty);
        }

        foreach (KeyValuePair<string, StringValues> queryParameter in
                 context.Request.Query.OrderBy(x => x.Key))
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
                           context.RequestAborted)) > 0)
            {
                hash.AppendData(
                    buffer.AsSpan(0, bytesRead));
            }
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        byte[] fingerprintBytes =
            hash.GetHashAndReset();

        return Convert.ToHexString(fingerprintBytes);
    }

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