# StackAlchemist: User Guide

Welcome to StackAlchemist! This guide will help you navigate the platform and generate your first production-ready application.

## 1. Choosing Your Mode

StackAlchemist offers two ways to define your application's architecture:

### Simple Mode (AI-First)
- **Best for:** Rapid prototyping or non-technical founders.
- **How it works:** Enter a natural language description (e.g., "A rental platform for professional cameras").
- **Result:** The AI generates a suggested database schema and API structure which you can then refine visually.

### Advanced Mode (Manual)
- **Best for:** Experienced developers who already have a schema in mind.
- **How it works:** Use the manual wizard to define every entity, field, and relationship.
- **Result:** You have complete control over every aspect of the data model from the start.

## 2. Understanding the "Compile Guarantee"

One of StackAlchemist's unique features is the **Compile Guarantee**. Before you receive your code, our engine:
1.  Reconstructs the repository in a secure, isolated environment.
2.  Runs a full build command (`dotnet build` and `npm run build`).
3.  If any errors occur, the AI automatically fixes the code and retries the build.
4.  Only once the build is successful is the zip archive prepared for you.

## 3. Tiers and Pricing

- **Tier 1 (Blueprint - $299):** Receive the architectural blueprints (JSON Schema, API Specs, and SQL scripts).
- **Tier 2 (Boilerplate - $599):** Receive the full source code for the .NET Backend, Next.js Frontend, and PostgreSQL Database.
- **Tier 3 (Infrastructure - $999):** Everything in Tier 2, plus AWS CDK scripts, Docker Compose files, and a step-by-step deployment runbook.

## 4. Post-Generation Steps

Once you download your `.zip` archive:
1.  **Extract the files** to your local development directory.
2.  **Environment Variables:** Copy `.env.example` to `.env.local` and fill in your Supabase credentials.
3.  **Supabase Link:** Use the Supabase CLI (`supabase link --project-ref your-project-id`) to connect to your database.
4.  **Launch:** Run `npm install` and `dotnet run` to start your application locally.
