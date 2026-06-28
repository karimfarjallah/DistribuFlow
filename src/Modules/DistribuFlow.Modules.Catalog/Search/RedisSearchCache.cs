using System.Text.Json;
using DistribuFlow.Modules.Catalog.Domain;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DistribuFlow.Modules.Catalog.Search;

/// <summary>
/// Cache-aside for search results. Degrades gracefully: if Redis is unavailable, search still
/// works straight from Elasticsearch (we just skip the cache).
/// </summary>
public sealed class SearchCache(IConnectionMultiplexer? redis, ILogger<SearchCache> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<Product>?> GetAsync(string key)
    {
        if (redis is null || !redis.IsConnected) return null;
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync($"search:{key}");
            return cached.HasValue
                ? JsonSerializer.Deserialize<List<Product>>((string)cached!)
                : null;
        }
        catch (Exception ex) { logger.LogDebug(ex, "Redis GET skipped"); return null; }
    }

    public async Task SetAsync(string key, IReadOnlyList<Product> products)
    {
        if (redis is null || !redis.IsConnected) return;
        try
        {
            var db = redis.GetDatabase();
            await db.StringSetAsync($"search:{key}", JsonSerializer.Serialize(products), Ttl);
        }
        catch (Exception ex) { logger.LogDebug(ex, "Redis SET skipped"); }
    }
}
