# Generate a full AI Real Estate Platform from a prompt

You describe the kind of real estate product your brokerage or marketplace needs. StackAlchemist generates the full .NET 10 + Next.js 16 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI real estate platform with:

- **Property listings** — `Listing`, `ListingMedia` and `ListingAttribute` entities with media, structured attributes and latitude/longitude
- **Agents and brokerages** — `Agent`, `Brokerage` and `AgentTransaction` entities with bios, license numbers and transaction history
- **Leads** — `Lead`, `LeadAssignment` and `LeadActivity` entities with source, assigned agent and follow-up history
- **Saved searches** — `Search`, `SearchAlert` and `SearchSubscriber` entities with price, bed and location criteria
- **Showings** — `Showing` and `ShowingRequest` entities with status and agent notes
- **MLS import tracking** — `MLSImport` and `MLSMapping` entities to record sync runs and field mappings
- **.NET 10 minimal API** — a Dapper repository and CRUD endpoints for every entity, documented with OpenAPI
- **Next.js 16 frontend** — TypeScript types, a typed API client and starter pages for your entities
- **PostgreSQL migration** — UUID keys, foreign keys, row-level security enabled (the policies are yours to write)
- **Docker Compose** for local development — `docker compose up` and you are running

**What you wire yourself:** the MLS feed (IDX, RETS or RESO, under your data agreement), map search and geo queries, alert and lead-notification emails, showing calendar invites, agent and buyer sign-in (the Supabase client is preinstalled; the auth flows are yours to write), the admin panel, and your CI pipeline. The entities carry the fields those features need; the feature code is yours.

Generation takes about 12 minutes from a single prompt. On the Boilerplate and Infrastructure tiers, the repo goes through `dotnet build` and `next build` before you can download it, and `build-report.json` in the archive records every command and its result.

## Why generate it instead of using a real estate platform

**Real estate platform vendors gatekeep your leads.** Zillow, Realtor.com, Redfin all sell back to agents the leads those agents originated. A brokerage running its own platform owns its leads outright. The economics flip the moment lead volume crosses a threshold.

**Off-the-shelf IDX sites are template skins.** Most boutique brokerage sites are a WordPress theme with an IDX widget bolted on. The brand is undifferentiated and the data is rented. Generated code is a real product, branded as your brokerage, with your search experience tuned to your market.

**Generic real estate sites can't encode your specialty.** Luxury brokerages, commercial sales, vacation rentals, foreclosure-focused brokerages, mobile-home parks — each has meaningfully different search filters, agent workflows, and lead-handling rules. A generated platform is tuned for your specialty from the prompt up.

## Who this is for

- **Independent brokerages** building an owned web presence to compete with the portal sites and the franchise brands.
- **Real estate marketplace founders** building specialty-vertical platforms (luxury, commercial, vacation rentals, distressed properties).
- **PropTech startups** building tools for agents, investors, or buyers who need a listings-and-leads foundation as the starting point for their actual product.
- **Investor groups** running internal platforms for portfolio property management with public-facing rent or sale listings.

## Example entities generated

A typical AI Real Estate Platform generation produces entities like:

- `Listing` / `ListingMedia` / `ListingAttribute`
- `Agent` / `Brokerage` / `AgentTransaction`
- `Lead` / `LeadAssignment` / `LeadActivity`
- `Search` / `SearchAlert` / `SearchSubscriber`
- `Showing` / `ShowingRequest`
- `MLSImport` / `MLSMapping`
- `User` / `Role`

The exact shape depends on your prompt. A residential brokerage generates different entities than a commercial leasing platform.

### Real example: Boutique luxury brokerage in a single metro

Imagine you submit this spec:

> "We run a boutique luxury brokerage in Miami. We list residential properties over $2M. Each listing has multiple photos, a video walkthrough URL, beds, baths, square footage, lot size, year built, and a custom 'luxury features' attribute (private dock, wine cellar, etc). Buyers can save listings, set up alerts for new listings matching their criteria, and request showings. Each request notifies the listing agent and our admin. We need a fast map-based search and a clean listing-detail page that respects high-end brand presentation."

StackAlchemist generates:

- `Listing` entity with property_type, price, beds, baths, sqft, lot_sqft, year_built, address, latitude, longitude, status (active, pending, sold, withdrawn), listed_at
- `ListingMedia` entity with listing_id, type (photo, video, virtual-tour), url, display_order
- `LuxuryFeature` enum / many-to-many table linking listings to features (private-dock, wine-cellar, smart-home, gated-community, etc.)
- `Agent` entity with name, bio, profile_photo, license_number, listings_count, total_volume_sold
- `Lead` entity with email, phone, listing_id (interest), assigned_agent_id, source, created_at
- `Showing` entity with listing_id, lead_id, scheduled_for, status, agent_notes
- `SearchAlert` entity with subscriber_email, price_range, beds_min, location_polygon, frequency
- `MLSImport` entity tracking the last sync timestamp and the count of listings imported per run
- CRUD endpoints for every entity (`/api/v1/listings`, `/api/v1/leads`, `/api/v1/showings`, …). A `GET /listings/search` endpoint with geo, price and attribute filters can be declared in Advanced Mode; the search query behind it is yours to write
- Next.js types and a typed API client for every entity, plus starter pages

That is the brokerage's data model and CRUD layer, compile-verified. The public site with map search (add PostGIS for the geospatial queries), the listing-detail presentation, the lead-capture flows, the agent dashboard, the brokerage admin panel and the MLS sync job are code you build on it. Docker Compose spins up a local PostgreSQL, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Sign your MLS data agreement and build the feed sync.** The generated repo has no MLS integration; it gives you `MLSImport` to track sync runs and `MLSMapping` to map MLS fields to your listing schema. Sign the data-use agreement with your local MLS, then write the scheduled job that pulls the feed (IDX, RETS or RESO Web API, depending on your region's standard) and upserts listings. This is the single thing that makes a real estate site a real estate site — without live MLS data you're a brochure.

2. **Add lead and showing notifications.** Lead notification and showing-request alerts are the two flows that drive revenue, and the generated repo does not send anything yet. Put a `NotificationProvider` interface in front of Resend or SendGrid for email and Twilio for SMS, and fire it when a `Lead` or `ShowingRequest` is created. Without these, leads sit in the database and nobody acts on them.

## What is not included

StackAlchemist is not Zillow. We don't host your platform, don't operate the MLS data agreements for you, and don't ship a managed mobile app. We generate you the code. You deploy and operate it.

We don't include comparable-property analytics, AVM (automated valuation models), or mortgage pre-qualification flows out of the box — those are dense feature areas with their own data dependencies. The platform you get is the listings, agents, and leads data layer, with tables ready to track your MLS sync. AVM and mortgage are downstream products you integrate once your platform has real listing volume.

## Pricing

One-time, per generation. Simple Mode (describe it in plain English) and Advanced Mode (define the entities step by step) are two ways to describe your app; the tier decides what you get.

- **Blueprint** — $299. `schema.json` (the entity-relationship model) and `api-docs.md` (the CRUD contract, endpoint by endpoint). Documents, no code.
- **Boilerplate** — $599. The repository described above, put through its real compilers before delivery (the Compile Guarantee).
- **Infrastructure** — $999. Boilerplate plus an AWS CDK stack, a Terraform AWS baseline, a Helm chart and a `DEPLOYMENT.md` runbook.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your real estate platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
