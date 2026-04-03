# Advanced Mode — The Entity Wizard

Advanced Mode gives you complete control over your schema. Instead of describing your application in prose, you define every entity, relationship, and API endpoint using a structured visual wizard.

---

## When to Use Advanced Mode

Advanced Mode is the right choice when:
- You have a clear data model already in mind
- You want explicit control over field names, types, and constraints
- You're building a complex system with many interdependencies
- You want to guarantee the schema matches your mental model exactly

For quick exploration or early-stage ideas, [Simple Mode](./simple-mode) may be faster.

---

## The Wizard Steps

Advanced Mode is a multi-step wizard. You can navigate between steps freely before finalizing.

### Step 1: Entities

Define each data entity in your system. Entities map directly to database tables and API controllers.

**For each entity, define:**
- **Name** — Singular noun (e.g., `User`, `Project`, `Invoice`)
- **Fields** — Column name, data type, and optional constraints

**Supported field types:**

| Type | Description | Example |
|------|-------------|---------|
| `string` | Variable-length text | name, email, slug |
| `text` | Long-form text | description, body, notes |
| `integer` | Whole number | count, position, quantity |
| `decimal` | Floating point number | price, latitude, rating |
| `boolean` | True/false flag | is_active, is_published |
| `datetime` | Date and time | created_at, due_date |
| `uuid` | UUID identifier | Automatically added to all entities |
| `enum` | Fixed set of values | status, priority, role |

**Standard fields added automatically:**
- `id` (UUID, primary key)
- `created_at` (timestamp)
- `updated_at` (timestamp)

---

### Step 2: Relationships

Define how entities relate to one another. Relationships generate foreign keys and navigation properties in the code.

**Relationship types:**

| Type | Example | Generated Output |
|------|---------|------------------|
| **One-to-Many** | One Project has many Tasks | FK on the "many" side |
| **Many-to-One** | Many Tasks belong to one Project | FK on the task table |
| **Many-to-Many** | Tasks can have many Tags | Junction table created |
| **One-to-One** | User has one Profile | FK with unique constraint |

**For each relationship, specify:**
- Source entity and target entity
- Relationship type
- Whether the FK is nullable (optional relationship) or required

---

### Step 3: API Endpoints

Define the REST API surface for each entity. StackAlchemist generates controller code for each endpoint you specify.

**Standard CRUD endpoints (enabled per entity):**

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/{entity}` | List all (with pagination) |
| `GET` | `/api/{entity}/{id}` | Get single by ID |
| `POST` | `/api/{entity}` | Create new |
| `PUT` | `/api/{entity}/{id}` | Update existing |
| `DELETE` | `/api/{entity}/{id}` | Delete by ID |

**Custom endpoints:**
You can also define custom action endpoints:

- `POST /api/orders/{id}/confirm` — Custom action
- `GET /api/projects/{id}/tasks` — Nested resource list
- `POST /api/users/{id}/invite` — Business action

For each custom endpoint, specify the HTTP method, path, and a brief description of its intent. The LLM uses this description to generate the method body.

---

### Step 4: Tier Selection and Review

Before generation, review your complete schema:

- All entities and fields are shown in a summary table
- All relationships are visualized as an ER diagram
- All API endpoints are listed

If anything needs adjusting, navigate back to the relevant step. Once you're satisfied, select a tier and proceed to checkout.

---

## Tips for Clean Schemas

### Use Consistent Naming
- Entity names: PascalCase singular (`BlogPost`, not `blog_posts` or `BlogPosts`)
- Field names: snake_case (`created_by`, `first_name`)
- Boolean fields: prefix with `is_` or `has_` (`is_active`, `has_paid`)

### Model Status Fields as Enums
Instead of a free-text `status` string, define it as an enum:
- `Order.status`: `pending | confirmed | shipped | delivered | cancelled`
- `Task.priority`: `low | medium | high | urgent`

This generates proper validation, constants, and type-safe enums in both C# and TypeScript.

### Don't Over-Engineer on Day One
The generated code is a starting point. You don't need to define every possible field in the wizard — define what's essential for V1. Adding fields and endpoints later is straightforward.

---

## What Gets Generated

Based on your entity wizard configuration, StackAlchemist generates:

**For each entity:**
- C# model class with data annotations
- Repository class with Dapper query implementations
- Controller with all specified endpoints
- TypeScript interface matching the model
- PostgreSQL migration with table, indexes, and constraints

**For each relationship:**
- Foreign key migrations
- Navigation properties in models
- Include logic in relevant queries

**For each custom endpoint:**
- Controller action method
- LLM-generated business logic in the service layer

---

## Related Docs

- [Simple Mode →](./simple-mode)
- [Getting Started →](./getting-started)
- [Understanding your output →](./your-output)
- [The Swiss Cheese Method (advanced) →](./swiss-cheese-method)
