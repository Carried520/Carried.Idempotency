namespace Carried.Idempotency.Redis.Scripts;

internal static class RedisScripts
{
    internal static readonly string Acquire = RedisScriptsLoader.Read("acquire");
    internal static readonly string Complete = RedisScriptsLoader.Read("complete");
    internal static readonly string Release = RedisScriptsLoader.Read("release");
    internal static readonly string RenewLease = RedisScriptsLoader.Read("renew_lease");
}