# Generate a full AI Inventory Management SaaS from a prompt

You describe the warehouse operation you run. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, models your SKUs, multi-location stock, and suppliers with CRUD endpoints for each, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI inventory management SaaS with:

- **SKU catalog** — `Sku`, `Variant` and `UnitOfMeasure` entities with codes, barcodes and reorder defaults
- **Multi-warehouse stock** — `Warehouse`, `Location`, `Bin` and `StockLevel` entities with quantity on hand, reserved and in transit per location
- **Stock movements** — a `StockMovement` entity recording each change with its type and reference
- **Suppliers and purchase orders** — `Supplier`, `PurchaseOrder`, `PurchaseOrderLine`, `Receipt` and `ReceiptLine` entities with expected dates and quantities received
- **Transfers** — `Transfer` and `TransferLine` entities with in-transit status
- **Costing data** — `CostLayer` and `ValuationSnapshot` entities for the valuation method you choose
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** the stock math (receipts and transfers updating `StockLevel`, partial receipts), reorder points and low-stock alerts, FIFO / LIFO / weighted-average costing, CSV import, the barcode-scanning page, the admin dashboard and valuation reports, sign-in and roles (the Supabase client is preinstalled; the auth flows are yours to write), and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of paying NetSuite

**NetSuite starts at $30k/year and goes up from there.** Once you add inventory, advanced warehouse management, and the per-user seats your ops team needs, you are looking at a six-figure annual contract with a 12-month minimum and an implementation partner you also have to pay. A StackAlchemist-generated codebase is $599 to $999 one time, and your warehouse team can be 5 people or 50 people for the same price.

**Fishbowl is $4500/year per seat.** That math works for a 2-person shop and breaks instantly for anyone running real warehouse operations. Three warehouse coordinators, a buyer, and a controller is $22,500/year forever. You own the code once and that number is zero.

**Cin7 and Zoho meter you on every axis.** Per-seat pricing, per-warehouse pricing, per-order tiers, and the inevitable "talk to sales" wall the second you outgrow the published plan. A generated codebase has no seats, no tiers, no usage caps. Add as many warehouses, SKUs, and scanner-toting receivers as your business actually needs.

## Who this is for

- **E-commerce DTC brands** running 3PL warehouses who want their own inventory source of truth instead of trusting a 3PL portal.
- **B2B distributors** with multiple branches who need real stock visibility across locations and a buyer workflow that does not cost $50k/year.
- **Manufacturers** tracking raw materials, WIP, and finished goods who need cost rollups without buying an ERP.
- **Operations leads** who want to evaluate a compile-verified inventory backbone before deciding whether to extend it or rip it out.

## Example entities generated

A typical AI inventory management generation produces entities like:

- `Sku` / `Variant` / `UnitOfMeasure`
- `Warehouse` / `Location` / `Bin`
- `StockLevel` / `StockMovement`
- `Supplier` / `PurchaseOrder` / `PurchaseOrderLine`
- `Receipt` / `ReceiptLine`
- `Transfer` / `TransferLine`
- `CostLayer` / `ValuationSnapshot`

The exact shape depends on your prompt. A DTC apparel brand with one 3PL generates a different graph than a 4-branch industrial distributor.

### Real example: B2B electrical distributor with 3 branches

Imagine you submit this spec:

> "We are an electrical wholesale distributor with 3 branches. We stock about 4,000 SKUs — wire, conduit, breakers, fittings. Each SKU has a primary supplier and a backup supplier with different lead times. We need per-branch stock levels, reorder points, and an automated buyer dashboard that shows what to reorder this week. Receivers scan barcodes on the dock. We use weighted-average costing. Transfers happen daily between branches."

StackAlchemist generates:

- `Sku` entity with code, description, primary_supplier_id, backup_supplier_id, unit_of_measure, default_reorder_point
- `Warehouse` entity per branch with code, name, address, default receiving location
- `StockLevel` entity with sku_id, warehouse_id, qty_on_hand, qty_reserved, qty_in_transit, reorder_point_override
- `StockMovement` entity recording every change with sku_id, warehouse_id, qty_delta, movement_type (receipt, sale, transfer_in, transfer_out, adjustment), reference_id
- `Supplier` entity with name, default_lead_time_days, payment_terms, contact info
- `PurchaseOrder` entity with supplier_id, warehouse_id (ship-to), status (draft, sent, partial, received, closed), expected_date
- `PurchaseOrderLine` entity with po_id, sku_id, qty_ordered, qty_received, unit_cost
- `Receipt` entity with po_id, received_at, received_by, with line-level fields for partial receipts
- `Transfer` entity with from_warehouse_id, to_warehouse_id, status (draft, in_transit, received), shipped_at, received_at
- `CostLayer` entity with sku_id, warehouse_id, qty, unit_cost, received_at — the data weighted-average rollups are computed from
- CRUD endpoints for every entity (`/api/v1/skus`, `/api/v1/receipts`, `/api/v1/transfers`, …). Endpoints like `GET /buyer-dashboard?warehouse=:id` (what to reorder this week) or `GET /valuation?as_of=:date` can be declared in Advanced Mode; the reorder and valuation logic behind them is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the inventory data model and CRUD layer, compile-verified. The admin and operator UI, the barcode-scanning page, the cost-layer math and the reorder-point logic are code you write on top. Docker Compose spins up PostgreSQL, the .NET API, and the Next.js frontend in one command so your developer can poke at it on day one.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Import your real SKU catalog and pilot one warehouse.** The repo gives you CRUD endpoints for `Sku` and `StockLevel`; write a short import script against them (or straight into Postgres). Export your current SKU master from whatever you are running today (NetSuite, Fishbowl, a spreadsheet), map columns, and load it. Pick your smallest warehouse, run weekly cycle counts against the generated stock levels for two weeks, and you have a real before/after comparison. You are now able to evaluate the generated system against your incumbent without paying anyone $30k.

2. **Wire your shipping carrier and your accounting system.** The generated code is not a black box — it is yours to modify. Add a `ShipStationClient` or an `EasyPostClient` to push outbound shipments. Add a nightly job that posts a journal entry to QuickBooks or Xero with the day's inventory movements and the weighted-average COGS for each shipment. These are the integrations every vendor charges extra for. You write them once against your own code and you are done.

## What is not included

StackAlchemist is not a full WMS. We do not include wave picking, slotting optimization, advanced shipping containers, or carrier rate-shopping out of the gate. We do not include EDI 850/856/810 trading-partner integration, because every distributor's EDI partners are different and the scaffolding is wasted tokens. We do not include a scanning UI: a browser-based page you build on the generated API works fine on a handheld Android scanner with a browser, and if you want a native Zebra app, you build it on top.

Inventory accuracy is your responsibility. The generated system gives you the `StockMovement` table and the CRUD endpoints to record counts and adjustments; the cycle-count workflow and the audit trail on every movement are yours to build. Neither makes your warehouse correct by magic. For 90% of distributors, manufacturers, and DTC brands, what you actually want is a system you own, with no per-seat tax and no vendor lock-in. For the 10% running automated AS/RS conveyors with deep WCS integration, buy Manhattan or Blue Yonder.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No per-seat charges. You own what you generate.

## Get started

Describe your warehouse operation in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
