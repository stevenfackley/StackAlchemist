# Simple Mode

Simple Mode is the fastest path from idea to architecture. You describe your SaaS in plain English — StackAlchemist handles the rest.

---

## When to Use Simple Mode

Use Simple Mode when:
- Your idea is still at the "concept" stage
- You haven't thought through every entity and relationship yet
- You want StackAlchemist to interpret your requirements and make sensible modeling decisions
- You want the fastest path to a generated codebase

Use [Advanced Mode](./advanced-mode) instead when you have a precise data model in mind and want explicit control over every entity, field, and endpoint.

---

## How It Works

1. Enter your application description in the prompt field on the home page
2. Click **Synthesize** or press `Ctrl + Enter`
3. StackAlchemist's LLM layer parses your description and produces a structured entity schema
4. You review the generated schema before committing to purchase
5. Select your tier and complete checkout
6. The generation pipeline runs and packages your output

---

## Writing a Good Prompt

The quality of your output is directly tied to the clarity of your description. Here are guidelines for getting the best results:

### Be Specific About Entities

Name the core "things" in your system. The LLM maps these to database tables and API controllers.

**Vague:**
> "A todo app"

**Better:**
> "A task management app with users, projects, tasks, and labels. Tasks belong to projects and can be assigned to users."

**Best:**
> "A task management SaaS. Users belong to workspaces. Workspaces have projects. Projects have tasks with fields: title, description, status (todo/in-progress/done), priority (low/medium/high/urgent), due date, and assignee. Tasks can have multiple labels. Users can comment on tasks."

---

### Describe Relationships

Explicitly state how entities connect to each other:

| Relationship Language | What It Maps To |
|-----------------------|-----------------|
| "Users belong to organizations" | FK: users.organization_id → organizations.id |
| "Projects have many tasks" | FK: tasks.project_id → projects.id |
| "Tasks can have multiple tags" | Junction table: task_tags |
| "Each order has one customer" | FK: orders.customer_id → customers.id |

---

### Mention Key Workflows

If there's a meaningful business process, describe it:

> "When an order is placed, inventory is decremented. If inventory reaches zero, the product is marked as out-of-stock."

The LLM uses this context to generate service logic in the appropriate layer.

---

### Field Types and Constraints

You don't need to be exhaustive, but hints help:

> "Products have a price (decimal), stock count (integer), and a published flag (boolean)."

---

## Example Prompts by Category

### E-commerce
```
A multi-vendor e-commerce platform. Vendors have stores. Stores have products
with categories, variants (size, color), and inventory per variant.
Customers place orders containing order items. Orders have a status workflow:
pending → confirmed → shipped → delivered. Payments are tracked per order.
```

### B2B SaaS
```
A CRM for small businesses. Organizations have team members with roles
(admin, member). Contacts belong to organizations. Deals are linked to contacts
and have stages (lead, qualified, proposal, won, lost) and a dollar value.
Activities (calls, emails, meetings) are logged against deals or contacts.
```

### Marketplace
```
A freelancer marketplace. Clients post projects with a budget and description.
Freelancers submit proposals with a price and cover letter. Clients select a
proposal to create a contract. Contracts track milestones and payments.
Both parties can leave reviews after contract completion.
```

---

## After Submission

After you submit a Simple Mode prompt, StackAlchemist will:

1. **Parse** — Extract entities, relationships, and field hints from your text
2. **Structure** — Build a normalized entity schema in JSON format
3. **Preview** — Show you the interpreted schema for review

At the review step you can see exactly what was extracted from your description before purchasing. If the interpretation is off, you can either refine your prompt or switch to Advanced Mode for manual correction.

---

## Limitations

- Simple Mode relies on LLM interpretation — ambiguous prompts may produce schemas you need to adjust
- Very complex systems with 15+ entities work better in Advanced Mode
- Domain-specific logic (complex validation rules, multi-step workflows) is better modeled explicitly in Advanced Mode

---

## Related Docs

- [Advanced Mode — Entity Wizard →](./advanced-mode)
- [Getting Started →](./getting-started)
- [Understanding your output →](./your-output)
