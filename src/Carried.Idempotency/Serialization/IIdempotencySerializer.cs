namespace Carried.Idempotency.Serialization;

/// <summary>
/// Defines serialization of values stored by the idempotency engine.
/// </summary>
public interface IIdempotencySerializer
{
    /// <summary>
    /// Serializes a value for storage by the idempotency engine.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to serialize.
    /// </typeparam>
    /// <param name="value">
    /// The value to serialize.
    /// </param>
    /// <returns>
    /// The serialized representation of the value.
    /// </returns>
    byte[] Serialize<T>(T? value);

    /// <summary>
    /// Deserializes a value previously stored by the idempotency engine.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to deserialize.
    /// </typeparam>
    /// <param name="payload">
    /// The serialized representation of the value.
    /// </param>
    /// <returns>
    /// The deserialized value.
    /// </returns>
    T? Deserialize<T>(byte[] payload);
}