namespace DistribuFlow.Modules.Catalog.Domain;

/// <summary>Product document as stored in Elasticsearch (the search projection).</summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<string> Tags { get; set; } = new();
}
