namespace Carried.Idempotency.Redis.Scripts;

internal static class RedisScriptsLoader
{
    private const string ResourcePrefix = "Carried.Idempotency.Redis.Scripts";
    
    internal static string Read(string resourceName)
    {
        var fullResourceName = $"{ResourcePrefix}.{resourceName}.lua";
        
        using Stream? resourceStream =
            typeof(RedisScriptsLoader).Assembly.GetManifestResourceStream(fullResourceName);
        using var streamReader = new StreamReader(
            resourceStream ?? throw new InvalidOperationException($"Could not find lua script - {resourceName}"));

        return streamReader.ReadToEnd();
    }
}