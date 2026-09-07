using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Carried.Idempotency.Redis.KeyBuilder;

internal static class RedisKeyMapper
{
    internal static RedisKey From(IdempotencyKey idempotencyKey)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        byte[] scope = Encoding.UTF8.GetBytes(idempotencyKey.Scope);
        byte[] keyValue = Encoding.UTF8.GetBytes(idempotencyKey.Value);

        BinaryPrimitives.WriteInt32BigEndian(length, scope.Length);
        hash.AppendData(length);
        hash.AppendData(scope);

        BinaryPrimitives.WriteInt32BigEndian(length, keyValue.Length);
        hash.AppendData(length);
        hash.AppendData(keyValue);

        byte[] result = hash.GetHashAndReset();

        return $"carried:idempotency:{Convert.ToHexString(result)}";
    }
}