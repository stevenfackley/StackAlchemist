# What I learned shipping 18 SaaS solution templates

**By Steve Ackley · June 1, 2026 · 9 min read**

Over the last two months I shipped 18 vertical "solutions" pages — each one a doorway into a specific kind of SaaS StackAlchemist can generate, from an [AI e-commerce platform](/solutions/ai-ecommerce-platform) to an [AI logistics tracking SaaS](/solutions/ai-logistics-tracking-saas). I expected the exercise to be marketing busywork. Instead, building the 18 taught me more about what the product actually is than building the generator did.

Here is what I learned, including the parts that are slightly embarrassing to admit.

## 1. Almost every vertical is an escape from a per-seat or commission tax

When I lined the pages up next to each other, the pattern was impossible to miss. Nearly every one positions against a named incumbent's pricing model:

- [AI customer support platform](/solutions/ai-customer-support-platform) → escape Zendesk's per-agent pricing
- [AI appointment booking SaaS](/solutions/ai-appointment-booking-saas) → escape Calendly's per-seat tax
- [AI fitness subscription platform](/solutions/ai-fitness-subscription-platform) → escape Mindbody's fees
- [AI food delivery platform](/solutions/ai-food-delivery-platform) → escape DoorDash's commission
- [AI event ticketing platform](/solutions/ai-event-ticketing-platform) → escape Eventbrite's per-ticket cut
- [AI HR onboarding platform](/solutions/ai-hr-onboarding-platform) → escape BambooHR's seat pricing
- [AI inventory management SaaS](/solutions/ai-inventory-management-saas) → escape NetSuite's per-seat licensing
- [AI CRM builder](/solutions/ai-crm-builder) → escape the Salesforce tax
- [AI project management SaaS](/solutions/ai-project-management-saas) → escape Jira's per-seat creep
- [AI analytics SaaS](/solutions/ai-analytics-saas) → own your product analytics instead of renting Amplitude

The common buyer isn't shopping for "an app." They've outgrown an incumbent's pricing curve and want off it. Owning the code converts a recurring per-seat or per-transaction bleed into a one-time cost. That reframed the whole product for me: we're not really selling software, we're selling an *exit from rent*. The [one-time pricing post](/blog/why-we-charge-once-not-monthly) was theory; the solutions pages are where it became concrete.

## 2. The 18 verticals collapse into about five shapes

By the time I'd modeled a dozen of these, I could see the skeletons underneath. Eighteen verticals are really about five recurring shapes:

- **Two-sided marketplace with payouts** — [marketplace](/solutions/ai-marketplace-platform), [food delivery](/solutions/ai-food-delivery-platform), [logistics](/solutions/ai-logistics-tracking-saas), [real estate](/solutions/ai-real-estate-platform)
- **Recurring subscription + scheduling** — [fitness](/solutions/ai-fitness-subscription-platform), [appointment booking](/solutions/ai-appointment-booking-saas), [LMS enrollments](/solutions/ai-lms-builder)
- **Queue + SLA + assignment** — [customer support](/solutions/ai-customer-support-platform), [ticketing](/solutions/ai-event-ticketing-platform), [project management](/solutions/ai-project-management-saas)
- **Catalog + inventory + order** — [e-commerce](/solutions/ai-ecommerce-platform), [inventory](/solutions/ai-inventory-management-saas)
- **Records + audit + compliance** — [healthcare](/solutions/ai-healthcare-patient-portal), [fintech](/solutions/fintech-saas-generator), [HR](/solutions/ai-hr-onboarding-platform)

This is exactly why the [Swiss Cheese method](/blog/swiss-cheese-method-deterministic-templates-llm-logic) works. The skeleton — auth, CRUD, Stripe, migrations, Docker, CI — is deterministic and reused across every shape. The LLM only fills the holes that are specific to the vertical. Shipping the eighteenth solution was dramatically cheaper than the first, because the shapes were already proven and the generator only had to reason about what made *this* domain different.

## 3. The compliance verticals quietly improved every other template

The two hardest verticals to ship were [healthcare](/solutions/ai-healthcare-patient-portal) and [fintech](/solutions/fintech-saas-generator). Both demanded something the easy verticals didn't: an immutable `AuditEvent` — a write-only record of every state change, the kind a HIPAA or financial auditor expects to see.

Once that primitive existed, I backported it. Now the [CRM](/solutions/ai-crm-builder) gets an audit trail on deal changes, the [inventory SaaS](/solutions/ai-inventory-management-saas) gets one on stock adjustments, and the [HR platform](/solutions/ai-hr-onboarding-platform) gets one on every personnel record edit. The most-regulated vertical raised the floor for all of them. The lesson generalizes: build for your strictest buyer first, and the rest inherit the rigor for free.

## 4. Entity modeling is where the LLM actually earns its money

A deterministic template can scaffold CRUD, wire auth, drop in Stripe, and write a Dockerfile. What it *cannot* do is know that a food-delivery `Order` needs a `Driver`, a `DeliveryZone`, and a `Payout`, while a [ticketing](/solutions/ai-event-ticketing-platform) `Order` needs a `SeatMap` and a `Scanner`, and a [logistics](/solutions/ai-logistics-tracking-saas) shipment needs a `ProofOfDelivery` and an `Exception`.

That domain modeling — choosing the right entities and relationships for *this* vertical — is precisely the gap the LLM fills. The template is the skeleton; the entity graph is the soul, and the soul is different for every domain. Watching the model get the entity graph right across 18 verticals is what convinced me the Swiss Cheese split is the correct architecture and not just a convenient one.

## 5. The honest part: the "AI" prefix is mostly SEO

Most of these pages say "AI [vertical]." Here's the truth I went back and forth on: the *generation* is AI. Most of the generated apps are ordinary, well-built CRUD SaaS with maybe one AI-assisted feature, not AI products in the "the app is an AI" sense.

I kept the "AI" prefix because it's what people actually type into a search box. But I'm not going to pretend that an [AI inventory management SaaS](/solutions/ai-inventory-management-saas) is a neural network with a warehouse attached. It's a real, owned inventory SaaS, *generated* by AI and compile-verified before you get it. Being straight about that matters more to me than a cleaner funnel. If the honesty costs me a few clicks, fine — it's the same honesty I bring to the [comparison pages](/compare/v0), and it's the reason I trust our own numbers.

## 6. Some of these are more proven than others

While I'm being candid: I'd stake real money on [e-commerce](/solutions/ai-ecommerce-platform), [CRM](/solutions/ai-crm-builder), [LMS](/solutions/ai-lms-builder), and [fintech](/solutions/fintech-saas-generator). Those shapes have been generated and pressure-tested the most.

The long tail — [logistics tracking](/solutions/ai-logistics-tracking-saas), [event ticketing](/solutions/ai-event-ticketing-platform) — I shipped because the underlying *shape* was proven (two-sided-with-payouts; queue-plus-SLA), not because I have fifty of each running in production. They're solid. They're just less battle-tested. If you generate one and hit a rough edge, that's exactly the signal I want — tell me, and it sharpens the template for the next person.

## What this means for you

If your idea is already on the [solutions list](/solutions), the shape is proven and the generator has paved that path. If it isn't, it's almost certainly a variant of one of the five skeletons above — and the generator handles variants of proven shapes far better than it handles novel ones.

The 18 pages were never 18 separate products. They're 18 doors into the same compile-gated generator, each one teaching me a little more about what we actually built.

## Key takeaways

- **The product is an exit from rent, not "an app."** Almost every vertical is an escape from a per-seat or commission tax. That's the real buyer motivation.
- **Eighteen verticals are five shapes.** The deterministic skeleton is reused; the LLM only fills the per-domain holes. This is the [Swiss Cheese method](/blog/swiss-cheese-method-deterministic-templates-llm-logic) paying off.
- **Build for your strictest buyer.** The audit-logging primitive that healthcare and fintech forced ended up improving every other template.
- **Entity modeling is the LLM's real job.** The skeleton is deterministic; the entity graph is what makes each vertical itself.
- **The "AI" prefix is SEO, and I'll say so.** The generation is AI; most generated apps are honest CRUD SaaS, and I'd rather tell you that than oversell.

If one of these shapes is your idea, [start a generation](/simple) and put the proven path to work. If you find an edge it doesn't cover, that's the email I most want to get.

— Steve
