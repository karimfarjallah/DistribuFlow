using DistribuFlow.Api;
using DistribuFlow.Common;
using DistribuFlow.Common.Behaviors;
using DistribuFlow.Modules.Catalog;
using DistribuFlow.Modules.Inventory;
using DistribuFlow.Modules.Orders;
using DistribuFlow.Modules.Shipping;
using FluentValidation;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// --- Modules (each registers its own DbContext + outbox processor) ---
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddShippingModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);

// --- Shared infrastructure: the in-process event bus (swap point for a real broker) ---
builder.Services.AddEventBus();

// --- CQRS: MediatR across all module assemblies + a validation pipeline behavior ---
var moduleAssemblies = new[]
{
    OrdersModule.Assembly, InventoryModule.Assembly, ShippingModule.Assembly, CatalogModule.Assembly
};
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(moduleAssemblies));
builder.Services.AddValidatorsFromAssemblies(moduleAssemblies);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddOpenApi();

var app = builder.Build();

// --- Create DBs, ES index, seed demo data ---
await DbInitializer.InitializeAsync(app.Services);

app.MapOpenApi(); // OpenAPI document at /openapi/v1.json

// Turn FluentValidation failures into a clean 400.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (ValidationException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { errors = ex.Errors.Select(e => e.ErrorMessage) });
    }
});

app.MapGet("/", () => Results.Ok(new
{
    service = "DistribuFlow",
    docs = "/openapi/v1.json",
    try_it = new[]
    {
        "GET  /catalog/products/search?q=laptop",
        "POST /orders  (triggers the saga: reserve stock -> ship -> complete)",
        "GET  /orders/{id}  (watch Status progress)",
        "GET  /inventory/stock",
        "GET  /shipping/shipments"
    }
})).WithTags("Root");

app.MapOrdersEndpoints();
app.MapInventoryEndpoints();
app.MapShippingEndpoints();
app.MapCatalogEndpoints();

app.Run();
