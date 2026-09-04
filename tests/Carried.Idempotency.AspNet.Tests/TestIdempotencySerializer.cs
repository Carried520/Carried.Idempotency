using System.Text.Json;
using Carried.Idempotency.Serialization;

namespace Carried.Idempotency.AspNet.Tests;

internal sealed class TestIdempotencySerializer :
    IIdempotencySerializer
{
    public byte[] Serialize<T>(T? value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    public T? Deserialize<T>(byte[] payload)
    {
        return JsonSerializer.Deserialize<T>(payload);
    }
}