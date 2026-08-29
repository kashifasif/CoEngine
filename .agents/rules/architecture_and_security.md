---
description: General architecture, database, and security best practices for this project.
---

# Architecture & Security Best Practices

When working on this project, adhere strictly to the following best practices established for production-readiness. **Do NOT use development-only shortcuts or "fake" implementations.**

## 1. Database & Entity Framework Core
- **Entity Framework Migrations:** Always use formal EF Core Migrations (`dotnet ef migrations add`) to manage schema changes. Do **not** use raw SQL `CREATE TABLE` scripts in `Program.cs` or manual data backfilling loops.
- **Primary Keys:** Always use `Guid` (UUID) for primary keys on all new tables to ensure security and prevent ID enumeration via HTTP requests. Avoid auto-incrementing integer IDs (`SERIAL`).
- **PostgreSQL Vector:** For vector embeddings, rely on the `pgvector` extension configured natively through EF Core (`HasPostgresExtension("vector")`).

## 2. Authentication & Authorization
- **HTTP-Only Cookies:** Never store sensitive authentication tokens in `localStorage` or `sessionStorage`. Always use secure, `HttpOnly` strict cookies to prevent XSS vulnerabilities.
- **Client Configuration:** Ensure the Blazor WebAssembly `HttpClient` is configured with `BrowserRequestCredentials.Include` (via a DelegatingHandler like `CookieHandler`) to securely transmit cookies.
- **Loosely Coupled Auth:** Use the `ICurrentUserService` abstraction to retrieve the authenticated user's details. This allows future SSO providers (e.g., Office365) to be implemented without refactoring controllers.
- **No Dev Fallbacks:** Do not include "fake" development fallbacks or bypasses (e.g., automatically logging in a default user if no token is found). Authentication must be real and enforced.
- **Strict Authorization:** Ensure all API endpoints are protected using the `[Authorize]` attribute by default.

## 3. Data Isolation (SaaS Architecture)
- **Tenant Isolation:** This is a multi-tenant SaaS application. Ensure strict data isolation by verifying that every database query filters by the `CreatedByUserId` or `UserId` of the currently authenticated user.
- **Prompt Isolation:** When sending data to LLMs, ensure that prompts only include context belonging to the current user. Data must never leak between users or projects.
