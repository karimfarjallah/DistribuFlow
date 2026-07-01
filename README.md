# DistribuFlow

An **event-driven modular monolith** in **.NET 10** that demonstrates four patterns end-to-end:

1. **CQRS + Vertical Slice Architecture** — every feature owns its command/query, handler, validator, and endpoint.
2. **Transactional Outbox** — domain events are written in the *same transaction* as the state change, then dispatched reliably.
3. **Saga (orchestration) with compensation** — a stateful coordinator drives `order → reserve stock → ship → complete`, and **cancels** the order if stock can't be reserved.
4. **Elasticsearch product search** — relevance-ranked, typo-tolerant search with a **Redis** cache-aside in front.

> No Azure required. The pieces Azure would normally provide are swapped for local equivalents you run with Docker Compose. Each swap is marked **SWAP POINT** in the code.

| Concern | Local (this repo) | Production swap |
|---|---|---|
| Messaging | In-process event bus, driven by the outbox | Azure Service Bus / RabbitMQ |
| Relational store | SQL Server 2022 (Docker) | Azure SQL |
| Cache | Redis (Docker) | Azure Cache for Redis |
| Search | Elasticsearch (Docker) | Elasticsearch / Elastic Cloud |

---

## Architecture at a glance

```
                         ┌─────────────────────────────────────────────┐
  HTTP  ── POST /orders ─▶│  Orders module                              │
                         │   CreateOrder (CQRS slice)                   │
                         │   Order aggregate ── raises OrderPlaced      │
                         │            │ (same tx)                       │
                         │            ▼                                 │
                         │      [ orders.OutboxMessages ]               │
                         └────────────┬────────────────────────────────┘
                                      │ OutboxProcessor (every ~2s)
                                      ▼
                           ┌──────────────────────┐
                           │  In-process EventBus  │  ← SWAP POINT (Service Bus / RabbitMQ)
                           └──────────┬───────────┘
                                      ▼
                         ┌─────────────────────────────────────────────┐
                         │  OrderFulfillmentSaga (in Orders)            │
                         │   OrderPlaced            → ReserveStock ─────┼──▶ Inventory module
                         │   StockReserved          → ArrangeShipping ──┼──▶ Shipping module
                         │   OrderShipped           → CompleteOrder      │
                         │   StockReservationFailed → CancelOrder (comp) │
                         └─────────────────────────────────────────────┘

  Catalog module (independent):  POST /catalog/products → Elasticsearch
                                 GET  /catalog/products/search → Redis cache-aside → Elasticsearch
```

Each module **owns its own database** (`DistribuFlow_Orders`, `DistribuFlow_Inventory`, `DistribuFlow_Shipping`) and its own outbox table. That isolation is what makes a future extraction to microservices mechanical rather than a rewrite.

---

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` → 10.x)
- **Docker Desktop** (for SQL Server, Elasticsearch, Redis)
- ~4 GB free RAM for the containers (Elasticsearch + SQL Server are the heavy ones)

> The SQL Server container image is x86-64. On Apple Silicon use Rosetta or substitute Azure SQL Edge; on a Windows/Linux x86 machine it runs as-is.

---

## Run it

```bash
# 1) Start infrastructure (SQL Server, Elasticsearch, Redis, Kibana)
docker compose up -d

# 2) Wait until containers are healthy (~30-40s for SQL Server's first boot)
docker compose ps

# 3) Run the API (creates databases, the ES index, and seeds demo data on startup)
dotnet run --project src/DistribuFlow.Api
```

The API starts on **http://localhost:5080**. Startup retries the DB connection, so if SQL Server is still booting it will wait rather than crash.

- OpenAPI document: `http://localhost:5080/openapi/v1.json`
- Quick index of demo endpoints: `http://localhost:5080/`
- Kibana (inspect the ES index): `http://localhost:5601`

---

## Demo script

Open `src/DistribuFlow.Api/DistribuFlow.Api.http` in VS / Rider / VS Code (REST Client) — or use the curl below.

**1 — Elasticsearch search (their domain):**
```bash
curl "http://localhost:5080/catalog/products/search?q=laptop"
curl "http://localhost:5080/catalog/products/search?q=keybord"   # typo → still finds the keyboard (fuzziness)
```

**2 — Happy-path saga (stock is available → order is completed):**
```bash
curl -X POST http://localhost:5080/orders -H "Content-Type: application/json" -d '{
  "customerName":"Acme Corp",
  "lines":[
    {"productId":"11111111-0000-0000-0000-000000000001","productName":"Pro Laptop 16","quantity":2,"unitPrice":2499},
    {"productId":"11111111-0000-0000-0000-000000000002","productName":"Wireless Mouse","quantity":2,"unitPrice":39}
  ]}'
# copy the returned id, then watch it progress (Pending → StockReserved → Completed)
curl http://localhost:5080/orders/THE_ID
```

**3 — Compensation (only 3 desks in stock, order 99 → saga cancels the order):**
```bash
curl -X POST http://localhost:5080/orders -H "Content-Type: application/json" -d '{
  "customerName":"Bulk Buyer",
  "lines":[{"productId":"11111111-0000-0000-0000-000000000003","productName":"Standing Desk","quantity":99,"unitPrice":599}]}'
curl http://localhost:5080/orders/THE_ID    # Status → Cancelled
```

**4 — See the effects:**
```bash
curl http://localhost:5080/inventory/stock      # QuantityReserved went up for the successful order
curl http://localhost:5080/shipping/shipments   # a shipment exists for the successful order
```

> Events flow on a ~2-second poll, so an order moves from *Pending* to *Completed* within a few seconds. Watch the **console logs** — every saga step logs a `[SAGA]` line, which is great to narrate live.

---

## Where each pattern lives (pattern → file)

| Pattern | Look here |
|---|---|
| CQRS command slice | `Modules/Orders/Features/CreateOrder.cs` (command + validator + handler + endpoint together) |
| CQRS query slice | `Modules/Orders/Features/GetOrder.cs` |
| Validation pipeline | `Common/Behaviors/ValidationBehavior.cs` |
| Aggregate raising events | `Modules/Orders/Domain/Order.cs` (`Order.Create` raises `OrderPlaced`) |
| Outbox write (same tx) | `Common/Outbox/OutboxInterceptor.cs` |
| Outbox dispatch | `Common/Outbox/OutboxProcessor.cs` (generic, one per module DbContext) |
| Event bus (swap point) | `Common/Messaging/InProcessEventBus.cs` |
| **Saga + compensation** | `Modules/Orders/Saga/OrderFulfillmentSaga.cs` |
| Idempotent consumer | `Modules/Inventory/Features/ReserveStock.cs` (checks existing reservation) |
| Elasticsearch search | `Modules/Catalog/Search/ElasticProductSearchIndex.cs` |
| Redis cache-aside | `Modules/Catalog/Search/RedisSearchCache.cs` + `Features/SearchProducts.cs` |
| Composition root | `DistribuFlow.Api/Program.cs` |

---

## Tests

```bash
dotnet test
```

Covers the order aggregate's behavior (total calculation, event raising, status transitions) and the outbox JSON round-trip that the dispatcher relies on. These run with **no infrastructure** — they're pure unit tests.

---

## Talking points

- **"I separate the write path from the read path."** Orders/Inventory/Shipping use SQL Server as the source of truth; the Catalog read model is Elasticsearch. That's exactly the shape of a search/recommendation product.
- **"I never lose an event."** The outbox writes the event in the same transaction as the state change, so a crash between commit and publish can't drop it — the dispatcher just picks it up on the next poll. At-least-once delivery, made safe by idempotent consumers.
- **"The saga makes a distributed workflow reliable without distributed transactions."** Orchestration keeps the flow readable and centralised; compensation (cancel the order when stock fails) handles the unhappy path explicitly.
- **"It's a modular monolith on purpose."** One deployable, simple ops — but module-owned data and integration-event boundaries mean any module can be lifted into its own service later by swapping the in-process bus for a real broker. No rewrite.
- **"Honest trade-off."** The read model is *eventually* consistent with the write side (a couple of seconds). For product search that's completely acceptable, and the win is that ingestion spikes never slow down search.

---

## Notes & honest caveats

- Package versions are pinned to known-good releases in `Directory.Packages.props`. If a `dotnet restore` complains about a specific version, bump that single line to the latest matching minor — the code doesn't depend on anything exotic.
- `EnsureCreated()` is used to create the schema for a frictionless demo. **Production uses EF migrations** (`dotnet ef migrations add` per module) for versioned, reviewable schema changes.
- Elasticsearch security and TLS are disabled in `docker-compose.yml` for local convenience only. Never do that in a real environment.
- Outbox messages are dispatched by polling. For higher throughput you'd add a `RowVersion`/`UPDATE ... OUTPUT` claim or push-based dispatch; polling is deliberately simple and obvious here.
