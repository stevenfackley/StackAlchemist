# Generate a full AI Real Estate Platform from a prompt

You describe the kind of real estate product your brokerage or marketplace needs. StackAlchemist generates the full .NET 10 + Next.js 15 + PostgreSQL codebase, verifies the build, and hands you the zip. You own the code. Deploy wherever you want.

## What you get

A production-shaped AI real estate platform with:

- **Property listings** with rich media, structured attributes, and geo coordinates
- **Agent profiles** with bios, listings, transaction history, and ratings
- **Lead capture** with form submissions, agent assignment, and follow-up reminders
- **Search and filters** indexed for fast retrieval — price, beds, baths, location, listing type
- **Saved searches and alerts** with email notifications on new matching listings
- **Showing scheduling** with agent availability, calendar invites, and confirmation
- **MLS sync scaffolding** for the IDX / RETS / RESO API integration your region uses
- **Admin panel** for agent management, listing moderation, and reporting
- **CI/CD** via GitHub Actions — lint, typecheck, unit tests, compile verification
- **Docker-compose** for local development — `docker compose up` and you are running

All of this is generated in about 12 minutes from a single prompt. Every build is verified with `dotnet build` + `pnpm build` before you can download.

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
- `SearchAlert` entity with subscriber_email, price_range, beds_min, location_polygon (PostGIS), frequency
- `MLSImport` entity tracking the last sync timestamp and the count of listings imported per run
- API endpoints: `GET /listings/search` (with geo, price, attribute filters), `POST /leads`, `POST /showings/request`, `GET /listings/:id`
- A Next.js public site with map search, listing detail, and lead-capture flows, plus an agent dashboard and brokerage admin panel
- A scheduled background job that polls the MLS feed and imports new listings, mapping MLS fields to your listing schema

All wired into a Next.js frontend and a .NET backend with PostGIS-based geospatial queries for fast map search. The generated CI/CD pipeline compiles and tests on every push. Docker-compose spins up a local PostgreSQL with PostGIS, the .NET API, and the Next.js frontend in one command.

## After you own the code: two next steps

Once the zip arrives and you have the repo cloned, here is what you do:

1. **Sign your MLS data agreement and wire the actual feed.** The generated repo includes the IDX / RETS / RESO API scaffolding (one of the three, depending on your region's standard), but you need to sign the data-use agreement with your local MLS and drop in the credentials. The sync job runs the moment credentials are in place. This is the single thing that makes a real estate site a real estate site — without live MLS data you're a brochure.

2. **Connect your transactional email and SMS provider.** Lead notification and showing-request alerts are the two flows that drive revenue. The generated repo includes the notification dispatch scaffolding pointed at a `NotificationProvider` interface; drop in Resend or SendGrid for email, Twilio for SMS, and the agent-side alerts fire automatically. Without these, leads sit in the database and nobody acts on them.

## What is not included

StackAlchemist is not Zillow. We don't host your platform, don't operate the MLS data agreements for you, and don't ship a managed mobile app. We generate you the code. You deploy and operate it.

We don't include comparable-property analytics, AVM (automated valuation models), or mortgage pre-qualification flows out of the box — those are dense feature areas with their own data dependencies. The platform you get is the listings, agents, and leads layer plus the MLS-sync scaffolding. AVM and mortgage are downstream products you integrate once your platform has real listing volume.

## Pricing

One-time, per generation:

- **Simple-mode real estate platform** — $299. Single brokerage, residential listings, basic search, lead capture, simple admin.
- **Blueprint-tier** — $599. Multi-agent, saved searches with email alerts, showing scheduling, MLS sync scaffolding, geo search.
- **Boilerplate-tier** — $999. Multi-brokerage, commercial property fields, advanced reporting on agent performance and lead source ROI, AVM integration scaffolding.

No monthly fee. No revenue share. You own what you generate.

## Get started

Describe your real estate platform in plain English. We generate the code. You own it.

**[Start generating →](/simple)**
