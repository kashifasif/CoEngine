# ⚡ SpecEngine — AI-Assisted Technical Specification Platform

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor WASM](https://img.shields.io/badge/Frontend-Blazor%20WebAssembly-512BD4?logo=blazor)](https://blazor.net)
[![EF Core SQLite](https://img.shields.io/badge/Database-SQLite%20EF%20Core-003B57?logo=sqlite)](https://docs.microsoft.com/ef/core/)
[![Vector Store RAG](https://img.shields.io/badge/AI-Vector%20DB%20Cosine%20RAG-10B981)](#-vector-database--cosine-similarity-rag)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **SpecEngine** is an enterprise-grade, high-density AI Specification Engine engineered for Agile Product Owners, Business Analysts, Software Architects, and QA Engineers. It streamlines the lifecycle of technical requirements—from interactive LLM brainstorming and vector similarity search to automated spec structuring, version diffing, and publish releases.

---

## 🌟 Key Features

### 1. 🎨 "Technical Precision" Multi-Page IDE UI
- **Projects Overview Dashboard (`/`):** View project spaces, active spec counts, scope tags, system health status (`Nominal`), and creation controls.
- **Specifications List (`/specs`):** High-density table grid displaying requirement IDs (`ID: LA-REQ-001`), published/draft status badges, version numbers, scope tag chips (`⚙️ api`, `🎨 mfe`, `🔒 security`), and action toolbars.
- **Spec Editor & Timeline Inspector (`/spec-editor/{id}` & `/versions`):** Rich editor canvas paired with a 320px right-hand Version History timeline tracking snapshot releases.

### 2. 💡 BA/PO Spec AI Workspace Studio (`/ai-workspace` & `/po-brainstorming`)
- **Split-Screen Workflow:** Interactive chat stream on the left paired with a **Live Spec Draft Preview** on the right.
- **Clean Plain-Text Token Streaming:** Real-time token streaming (`IAsyncEnumerable<string>`) over HTTP via OpenRouter (`z-ai/glm-5.2:free`).
- **One-Click AI Spec Structuring:** Convert unstructured chat conversations into structured titles, user story descriptions, and Bento-style acceptance criteria with a single click (*"Structure into Spec"*).

### 3. 🔍 Published Specs Q&A Studio (`/published-specs-qa`)
- **Developer Mode:** Query published requirements for microservice boundaries, API payload contracts, database schema impacts, and HTTP error status codes.
- **QA Engineer Mode:** Generate Given-When-Then Gherkin test scenarios, boundary value edge cases, and QA regression test checklists.

### 4. ⚡ Local Vector Database & Cosine Similarity RAG (`IVectorStoreService`)
- Implements an in-memory/local vector database computing **Cosine Similarity Vectors**:
  $$\text{CosineSimilarity}(\vec{A}, \vec{B}) = \frac{\vec{A} \cdot \vec{B}}{\|\vec{A}\| \|\vec{B}\|}$$
- **Unified Knowledge Base:** Indexes both **Published Specifications** and **Brainstorming Chat Sessions**.
- **Pluggable Architecture:** Interface `IVectorStoreService` allows seamless swapping for Pinecone, Qdrant, Weaviate, PgVector, or Milvus in production.

### 5. 💾 EF Core Database Chat & Release Persistence
- Transcripts for all AI persona sessions are saved directly to SQLite tables (`ChatSessions` & `ChatMessages`) linked to `ProjectId` and `PersonaMode`.
- Full project history persists across page refreshes and team browser sessions.

---

## 🏗️ Architecture & Project Structure

The repository is structured as a clean .NET 10 solution:

```
SpecEngine/
├── src/
│   ├── SpecPlatform.Api/            # ASP.NET Core Web API & Blazor WASM Host Server
│   │   ├── Controllers/             # SpecsController (REST, Streaming, RAG endpoints)
│   │   ├── Data/                    # AppDbContext (EF Core SQLite schema & migrations)
│   │   ├── Services/                # OpenRouterService & LocalVectorStoreService
│   │   └── Program.cs               # Blazor static web asset hosting & CORS configuration
│   ├── SpecPlatform.Client/         # Blazor WebAssembly WASM SPA Client
│   │   ├── Layout/                  # MainLayout (TopNavBar & 280px Fixed SideNavBar)
│   │   ├── Pages/                   # Home, SpecsList, AiWorkspace, DevQaAssistant, SpecEditor, VersionHistory
│   │   ├── Services/                # SpecApiClient (HTTP & Streaming Service)
│   │   └── wwwroot/css/app.css      # "Technical Precision" Design System CSS
│   └── SpecPlatform.Shared/         # Shared DTOs & Domain Entities
│       ├── DTOs/                    # Project, Spec, Chat, Notification & Vector DTOs
│       └── Models/                  # Project, Spec, SpecVersion, AcceptanceCriterion, ChatSession, VectorRecord
├── tests/
│   └── SpecPlatform.Tests/          # xUnit Integration Test Suite (6/6 passing tests)
├── theme/                           # UI Figma/HTML prototypes & DESIGN.md guidelines
└── SpecPlatform.slnx                # Solution Manifest
```

---

## 🚀 Quick Start Guide

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 1. Clone & Build
```bash
git clone https://github.com/kashifasif/SpecEngine.git
cd SpecEngine
dotnet build
```

### 2. Run Automated Integration Tests
```bash
dotnet test
```

### 3. Run the Application
Launch the unified server host (API + Blazor WASM Frontend):
```bash
dotnet run --project src/SpecPlatform.Api/SpecPlatform.Api.csproj --urls "http://localhost:5005"
```

Open your browser at **`http://localhost:5005`** to launch SpecEngine!

Open your browser and navigate to:
👉 **`http://localhost:5005`**

---

## 🔑 Environment & API Configuration

Configure your OpenRouter API key in `src/SpecPlatform.Api/appsettings.json` or via environment variable:

```json
{
  "OpenRouter": {
    "ApiKey": "YOUR_OPENROUTER_API_KEY",
    "Model": "z-ai/glm-5.2:free"
  }
}
```

Or set the environment variable:
```bash
export OPENROUTER_API_KEY="sk-or-v1-your-key-here"
```

---

## 🔌 API Reference

| HTTP Method | Endpoint Path | Description |
| :--- | :--- | :--- |
| `GET` | `/api/health` | Service health status |
| `GET` | `/api/projects` | List all project spaces |
| `POST` | `/api/projects` | Create a new project space |
| `GET` | `/api/projects/{id}/specs` | List specifications for project |
| `POST` | `/api/projects/{id}/specs` | Create a new specification draft |
| `POST` | `/api/specs/{id}/publish` | Publish new immutable spec version & create audit notification |
| `GET` | `/api/specs/{id}/versions/{v1}/diff/{v2}` | Calculate side-by-side release diff |
| `GET` | `/api/projects/{id}/chat-session/{persona}` | Get persistent DB conversation transcript |
| `POST` | `/api/projects/{id}/chat-session/{persona}/messages` | Save chat messages to EF Core DB & index into Vector Store |
| `POST` | `/api/specs/draft/chat/stream` | Stream AI brainstorming responses (`text/plain; charset=utf-8`) |
| `POST` | `/api/specs/query/chat/stream` | Stream QA/Dev Q&A grounded in Vector Store Cosine Similarity RAG |
| `GET` | `/api/projects/{id}/vector-store` | Retrieve live vector indexing statistics |

---

## 📄 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
