using System.Text.Json;

namespace Carried.Idempotency.Serialization;

internal class JsonIdempotencySerializer : IIdempotencySerializer
{
    public byte[] Serialize<T>(T? value)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
        return payload;
    }

    public T? Deserialize<T>(byte[] payload)
    {
        var value = JsonSerializer.Deserialize<T>(payload);
        return value;
    }
}