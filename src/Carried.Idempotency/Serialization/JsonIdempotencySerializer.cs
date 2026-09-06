using System.Text.Json;

namespace Carried.Idempotency.Serialization;

internal sealed class JsonIdempotencySerializer : IIdempotencySerializer
{
    public byte[] Serialize<T>(T? value) => JsonSerializer.SerializeToUtf8Bytes(value);

    public T? Deserialize<T>(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return JsonSerializer.Deserialize<T>(payload);
    }
}