namespace Carried.Idempotency.Serialization;
/// <summary>
/// Defines serialization of values stored by the idempotency engine.
/// </summary>
public interface IIdempotencySerializer
{
    byte[] Serialize<T>(T? value);
    T? Deserialize<T>(byte[] payload);
}