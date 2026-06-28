using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using DistribuFlow.Modules.Catalog.Domain;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Modules.Catalog.Search;

/// <summary>
/// Elasticsearch-backed product search. Writes go to MSSQL as the source of truth in the full
/// system; here the IndexProduct command is the ingestion step that projects into ES.
/// Search uses multi_match with fuzziness and a boost on the product name.
/// </summary>
public sealed class ElasticProductSearchIndex(ElasticsearchClient client, ILogger<ElasticProductSearchIndex> logger)
    : IProductSearchIndex
{
    public const string IndexName = "products";

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        var exists = await client.Indices.ExistsAsync(IndexName, ct);
        if (exists.Exists) return;

        var created = await client.Indices.CreateAsync(IndexName, ct);
        if (!created.IsValidResponse)
            logger.LogWarning("Could not create ES index '{Index}': {Error}", IndexName, created.DebugInformation);
        else
            logger.LogInformation("Created Elasticsearch index '{Index}'", IndexName);
    }

    public async Task IndexAsync(Product product, CancellationToken ct = default)
    {
        var response = await client.IndexAsync(product, i => i.Index(IndexName).Id(product.Id.ToString()), ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Failed to index product: {response.DebugInformation}");
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(string query, int size, CancellationToken ct = default)
    {
        var response = await client.SearchAsync<Product>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .MultiMatch(mm => mm
                    .Query(query)
                    .Fields("name^3,description,category,tags")
                    .Fuzziness(new Fuzziness("AUTO")))), ct);

        if (!response.IsValidResponse)
        {
            logger.LogWarning("Search failed: {Error}", response.DebugInformation);
            return Array.Empty<Product>();
        }
        return response.Documents.ToList();
    }
}
