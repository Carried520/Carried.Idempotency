namespace Carried.Idempotency.Serialization;

public interface IIdempotencySerializer
{
    byte[] Serialize<T>(T? value);
    T? Deserialize<T>(byte[] payload);
}