# StackAlchemist: The Intelligent Software Architect

![License: Proprietary](https://img.shields.io/badge/License-Proprietary-red.svg)
![Stack: .NET 10 | Next.js 15 | Supabase](https://img.shields.io/badge/Stack-.NET%2010%20%7C%20Next.js%2015%20%7C%20Supabase-blue)

StackAlchemist is an enterprise-grade micro SaaS platform that automates the scaffolding of production-ready software architectures. It transmutes natural language prompts or visual schemas into fully functional, build-guaranteed repositories.

---

## ⚠️ License Notice

StackAlchemist is a **Proprietary & Source Available** product. While the source code is public for transparency and community forks, commercial use is restricted to paid license holders. 

- **For Personal Use:** You are free to fork and explore the codebase.
- **For Commercial Use:** You must purchase a tier from [StackAlchemist.app](https://stackalchemist.app).

Please see the [LICENSE](LICENSE) file for full details.

## 🚀 Key Features

- **"Swiss Cheese" Generation:** Combines high-quality static templates with LLM-generated business logic for 100% reliable builds.
- **Compile Guarantee:** Every generated repository is built locally before delivery, ensuring zero hallucinations.
- **Dual Mode Intake:** Toggle between a natural language "Simple Mode" and a structured "Advanced Mode" schema builder.
- **Tiered Delivery:** From simple blueprints (Tier 1) to full codebases (Tier 2) and production IaC runbooks (Tier 3).

## 🛠️ Tech Stack

- **Frontend:** Next.js (App Router), Tailwind CSS, shadcn/ui.
- **Backend:** .NET 10 Web API, Dapper Micro ORM.
- **Database:** Supabase PostgreSQL.
- **Intelligence:** Claude 3.5 Sonnet (via Anthropic API or BYOK).
- **Storage:** Cloudflare R2 (Zero Egress).

## 📂 Repository Structure

- `src/StackAlchemist.Web/`: Next.js frontend and API orchestration.
- `src/StackAlchemist.Engine/`: The "Swiss Cheese" generation and reconstruction engine.
- `src/StackAlchemist.Worker/`: Background service for build validation and archive packing.
- `src/StackAlchemist.Templates/`: Master boilerplate library for the generation engine.
- `docs/`: Comprehensive technical and user documentation.

## 🔧 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js (v20+)](https://nodejs.org/)
- [Supabase CLI](https://supabase.com/docs/guides/cli)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/steveackley/StackAlchemist.git
   cd StackAlchemist
   ```
2. Install dependencies:
   ```bash
   npm install
   dotnet restore
   ```
3. Set up environment variables:
   ```bash
   cp .env.example .env.local
   ```
4. Start development server:
   ```bash
   npm run dev
   ```

## 📖 Documentation

### User & Support
- [User Guide](docs/user/user-guide.md)
- [Troubleshooting](docs/user/troubleshooting.md)

### Product & Business
- [Business Requirements Document](docs/product/Business%20Requirements%20Document.md)
- [Market Requirements Document](docs/product/Market%20Requirements%20Document.md)
- [Product Requirements Document](docs/product/Product%20Requirements%20Document.md)
- [Product Design Document](docs/product/Product%20Design%20Document.md)
- [ASCII Wireframes](docs/product/wireframes.md)

### Engineering & Architecture
- [Software Design Document](docs/architecture/Software%20Design%20Document.md)
- [Data Flow Diagram](docs/architecture/Data%20Flow%20Diagram.md)
- [Sequence Diagram](docs/architecture/Sequence%20Diagram.md)
- [Database ERD](docs/architecture/Database%20ERD.md)
- [Generation State Machine](docs/architecture/Generation%20State%20Machine.md)

## 🤝 Contributing

Please see our [Contributing Guide](.github/CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.
