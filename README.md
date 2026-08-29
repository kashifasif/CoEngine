# ⚡ CoEngine (Community Edition)

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor WASM](https://img.shields.io/badge/Frontend-Blazor%20WebAssembly-512BD4?logo=blazor)](https://blazor.net)
[![PostgreSQL + pgvector](https://img.shields.io/badge/Database-PostgreSQL%20%2B%20pgvector-336791?logo=postgresql)](https://github.com/pgvector/pgvector)
[![Tailwind CSS v4](https://img.shields.io/badge/Styling-Tailwind%20CSS%20v4-38B2AC?logo=tailwindcss)](https://tailwindcss.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Open Source](https://img.shields.io/badge/Edition-Community%20(Open%20Source)-blue.svg)](#-license)

> **CoEngine** is an open-source, AI-powered Technical Specification & Requirements Engineering Platform designed for **Product Owners, Business Analysts, Software Architects, and QA/Dev Teams**. It bridges the gap between messy brainstorming and production-ready technical specifications through structured AI ideation, vector-grounded RAG, immutable versioning, and developer copilots.

---

## 📖 What is CoEngine?

Building software requires clear, unambiguous, and traceable requirements. However, initial requirements often begin as unstructured notes, slack threads, or discovery calls. 

**CoEngine** streamlines this entire lifecycle:
1. **Interactive Clarification:** Transforms raw thoughts and meeting transcripts into structured user stories, acceptance criteria, and architecture tags.
2. **Deterministic Versioning:** Maintains immutable version histories with side-by-side visual diffs and instant undo/redo capabilities.
3. **Developer & QA Grounding:** Features an integrated Dev & QA Copilot powered by Retrieval-Augmented Generation (RAG) that answers questions strictly from published specifications.
4. **Tool Interoperability:** Includes an MCP (Model Context Protocol) server to expose your specifications directly to external IDEs and AI coding tools (e.g., Cursor, Claude Desktop).

---

## 🔄 How CoEngine Works

```
┌─────────────────────────┐      ┌──────────────────────────┐      ┌─────────────────────────┐
│ 1. Brainstorming Studio │ ───> │ 2. Structuring & Review  │ ───> │ 3. Immutable Publishing │
│  - Raw Transcript Import│      │  - Acceptance Criteria   │      │  - Version Snapshots    │
│  - Interactive MCQs     │      │  - Architecture Scope Tag│      │  - Visual Diffing & Logs│
│  - Clarification Rounds │      │  - Self-Review Quality   │      │  - Audit History        │
└─────────────────────────┘      └──────────────────────────┘      └─────────────────────────┘
                                                                                │
                                                                                ▼
┌─────────────────────────┐      ┌──────────────────────────┐      ┌─────────────────────────┐
│  6. MCP Server Protocol │ <─── │ 5. Dev & QA Copilot RAG  │ <─── │ 4. Vector Store Index   │
│  - Cursor & Claude sync │      │  - Microservice schemas  │      │  - pgvector Embeddings  │
│  - Real-time spec feeds │      │  - Gherkin test matrices │      │  - Grounded Contexts    │
└─────────────────────────┘      └──────────────────────────┘      └─────────────────────────┘
```

### 1. 🧠 Phase 1: AI Workspace & Clarification Studio
- **Transcript Ingestion:** Upload meeting recordings, interview transcripts, or raw bullet points.
- **Smart Clarifying Questions:** The AI asks targeted multiple-choice questions (MCQs) to resolve ambiguities.
- **Enforced Round Limits:** Supports configurable clarification limits (e.g., max 3 rounds) to prevent endless loops, while allowing manual overrides anytime.
- **Answer Locking:** Once a specification is published, previous questions and answers are locked to preserve the exact audit history of decisions made.

### 2. 📋 Phase 2: Automated Structuring & Self-Review
- **One-Click Structuring:** Formats unstructured brainstorming into a clean title, description, bento-style acceptance criteria, and scope tags (`bff`, `api`, `mfe`, `security`).
- **Automated Quality Checks:** Scans drafts for placeholders (`TODO`, `TBD`, `later`), internal inconsistencies, and missing edge cases before publishing.

### 3. ✍️ Phase 3: Optimistic Concurrency Spec Editor
- **Collaborative Draft Editing:** Edit draft criteria and scope tags directly in the workspace.
- **Concurrency Control:** Utilizes `RowVersion` concurrency checks so concurrent edits are detected and prevented from silently overwriting each other.

### 4. 📦 Phase 4: Immutable Versioning & Release Auditing
- **Snapshot Releases:** Every publish generates an immutable version (`v1`, `v2`, etc.).
- **Side-by-Side Diff Viewer:** Inspect exactly what changed between any two versions with granular line-level diffs.
- **Undo / Redo Rollbacks:** Roll back or restore published versions with a single click.
- **Chat Audit Stream:** Automatically records version publication timestamps and summaries in the project history.

### 5. 🤖 Phase 5: Dev & QA Copilot (Vector RAG)
- **Zero Hallucination Grounding:** Uses vector embeddings (via PostgreSQL `pgvector`) to answer queries *strictly* based on published specifications.
- **Developer Mode:** Queries API payload schemas, database impacts, microservice boundaries, and error codes.
- **QA Engineer Mode:** Automatically synthesizes Given-When-Then Gherkin test scenarios, edge-case test matrices, and regression checklists.

### 6. 🔌 Phase 6: Model Context Protocol (MCP) Server
- Includes a built-in MCP server (`/mcp`) that exposes your team's specifications to AI-assisted IDEs like **Cursor**, **Windsurf**, and **Claude Desktop**.

---

## 🏛️ Technology Stack

| Layer | Technologies |
| :--- | :--- |
| **Frontend** | Blazor WebAssembly (.NET 10), Tailwind CSS v4, Vanilla CSS Custom Design System |
| **Backend API** | ASP.NET Core Web API (.NET 10), Minimal APIs & Controllers, Semantic Kernel |
| **Database & ORM** | PostgreSQL with `pgvector` extension (also supports SQLite), Entity Framework Core |
| **AI & LLM** | OpenRouter (`anthropic/claude-3.5-sonnet`, `glm-5.2`, etc.), Semantic Kernel streaming |
| **Security & Auth** | Strict `HttpOnly` Secure Cookie Authentication (Zero client-side token exposure), Guid PKs |
| **Protocol** | Model Context Protocol (MCP) Server for IDE tool integration |

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://www.docker.com/) (for PostgreSQL + pgvector)

### 1. Clone the Repository
```bash
git clone https://github.com/kashifasif/CoEngine.git
cd CoEngine
```

### 2. Start PostgreSQL with pgvector
```bash
docker compose up -d db
```

### 3. Configure API Keys
Set your OpenRouter API Key in `src/CoEngine.Api/appsettings.json` or via environment variables:

```bash
export OpenRouter__ApiKey="sk-or-v1-your-openrouter-key"
```

### 4. Run the Application
```bash
# Run backend API and Blazor WASM host
dotnet run --project src/CoEngine.Api --urls "http://localhost:5005"
```

Open your browser at **`http://localhost:5005`** to launch the CoEngine dashboard!

---

## 🐳 Docker Deployment

To run the entire platform (Database, API, and Client) via Docker Compose:

```bash
docker compose up -d --build
```

Access the application at `http://localhost:5005`.

---

## 🧪 Testing

Run the automated integration test suite:

```bash
dotnet test
```

---

## 📄 License & Commercial Use

This project is open source and available under the **[MIT License](LICENSE)**.

**Community Edition Benefits:**
- ✅ **Free for Personal, Educational, and Commercial Use.**
- ✅ **Modify, fork, and distribute freely.**
- ✅ **Self-host in your own infrastructure with complete data privacy.**

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request or open an Issue for bug reports and feature ideas.
