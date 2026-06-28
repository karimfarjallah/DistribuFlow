using DistribuFlow.Modules.Catalog.Features;
using DistribuFlow.Modules.Catalog.Search;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DistribuFlow.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration config)
    {
        // Elasticsearch client (security disabled locally; see docker-compose).
        var esUrl = config["Elasticsearch:Url"] ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(esUrl)).DefaultIndex(ElasticProductSearchIndex.IndexName);
        services.AddSingleton(new ElasticsearchClient(settings));
        services.AddSingleton<IProductSearchIndex, ElasticProductSearchIndex>();

        // Redis (optional; abortConnect=false so the app starts even if Redis is down).
        var redisCfg = config.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
        try
        {
            var mux = ConnectionMultiplexer.Connect(redisCfg);
            services.AddSingleton<IConnectionMultiplexer>(mux);
        }
        catch
        {
            services.AddSingleton<IConnectionMultiplexer?>(_ => null);
        }
        services.AddSingleton<SearchCache>();

        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapIndexProduct();
        app.MapSearchProducts();
        return app;
    }

    public static System.Reflection.Assembly Assembly => typeof(CatalogModule).Assembly;
}
