using DistribuFlow.Modules.Catalog.Domain;

namespace DistribuFlow.Modules.Catalog.Search;

public interface IProductSearchIndex
{
    Task EnsureCreatedAsync(CancellationToken ct = default);
    Task IndexAsync(Product product, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> SearchAsync(string query, int size, CancellationToken ct = default);
}
