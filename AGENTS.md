# AGENTS.md - GymApp Development Guide

## Project Overview

GymApp is a multi-tenant SaaS platform for gym/fitness management built with:
- **Backend**: .NET 10 Minimal API with PostgreSQL (EF Core 10)
- **Frontend**: Vanilla HTML/CSS/JS with ES Modules (no bundler)
- **Infrastructure**: Docker, Docker Compose, Nginx

Multi-tenant architecture resolves tenant via: Custom Domain > Subdomain > X-Tenant-Slug header.

---

## Build & Run Commands

### Docker (Full Stack)
```bash
docker compose up --build -d           # Build and run all services
docker compose up --build -d api       # Run only API
docker compose down                     # Stop all services
```

### Local Backend Development
```bash
cd backend
dotnet run --project GymApp.Api                          # Run API on http://localhost:5000
dotnet run --project GymApp.Api --urls "http://localhost:5000"
```

### Database Migrations (EF Core)
```bash
cd backend
dotnet ef migrations add <MigrationName> --project GymApp.Infra --startup-project GymApp.Api
dotnet ef database update --project GymApp.Infra --startup-project GymApp.Api
dotnet ef migrations list --project GymApp.Infra --startup-project GymApp.Api
```

### Frontend Development
Serve `frontend/` directory with any static server (VS Code Live Server, etc.). Default port 8095.

---

## Code Style Guidelines

### C# Conventions (.NET 10)

**File Structure**
- File-scoped namespaces: `namespace GymApp.Api.Endpoints;`
- One class/record per file, filename matches type name
- Order: namespaces → using → type declaration

**Naming**
- Types/Methods/Properties: `PascalCase`
- Local variables/parameters: `camelCase`
- Private fields: `_camelCase` (when used)
- Enums: `PascalCase` values

**DTOs**
- Use C# records with primary constructors: `public record LoginRequest(string Email, string Password);`
- Response records include all serialized fields explicitly

**Entities**
- Primary key: `Guid Id { get; set; } = Guid.NewGuid();`
- String defaults: `string Name { get; set; } = string.Empty;`
- Nullable: `string? LogoUrl { get; set; }`
- Navigation properties: Initialize collections: `public ICollection<User> Users { get; set; } = [];`

**Computed Properties**
- Use expression-bodied properties for derived state: `public bool IsActive => Status == ActiveStatus;`
- Avoid methods for simple computed values

**Nullable Reference Types**
- Enabled in all projects (`<Nullable>enable</Nullable>`)
- Use `?` for nullable reference types
- Initialize non-nullable strings with `string.Empty`

**API Endpoints (Minimal API)**
```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");
        
        group.MapPost("/login", async (LoginRequest req, AppDbContext db) =>
        {
            // Handler logic
            return Results.Ok(response);
        }).AllowAnonymous();
        
        group.MapGet("/me", () => { ... }).RequireAuthorization();
    }
}
```

**Async/Await**
- Always use async for I/O operations (DB, HTTP, file)
- Use `async Task<T>` return types, not `async void`

**Error Handling**
- Return appropriate `Results` (Ok, BadRequest, NotFound, Unauthorized, etc.)
- Include meaningful error messages: `return Results.BadRequest("User not found.");`

---

### JavaScript Conventions (ES Modules)

**Naming**
- Functions/variables: `camelCase`
- Constants: `SCREAMING_SNAKE_CASE`
- File naming: `kebab-case.js` (e.g., `api-client.js`)

**Imports**
- ES Module syntax: `import { api } from './api.js';`
- Export object for API methods

**Pattern for API Client**
```javascript
const API_BASE = '/api';

async function request(path, options = {}) {
    const headers = { 'Content-Type': 'application/json', ...options.headers };
    // Add auth token and tenant header
    const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
    if (!res.ok) throw new ApiError(res.status, await res.text());
    return res.json();
}

export const api = {
    get: (path) => request(path),
    post: (path, body) => request(path, { method: 'POST', body: JSON.stringify(body) }),
    // ...
};
```

**DOM Manipulation**
- Use vanilla JS, avoid frameworks
- CSS selectors for element selection
- CSS variables for theming

---

## Architecture

### Backend Structure
```
backend/
├── GymApp.Domain/           # Entities, Enums, Interfaces (no external deps)
│   ├── Entities/            # Domain models
│   ├── Enums/                # Enumerations
│   └── Interfaces/           # Service interfaces
├── GymApp.Infra/            # Data access, services
│   └── Data/                # AppDbContext, Migrations, SeedData
└── GymApp.Api/              # Minimal API, Endpoints, DTOs
    ├── Endpoints/           # Route handlers
    ├── DTOs/                # Request/Response records
    ├── Middleware/          # TenantMiddleware, SubscriptionMiddleware
    ├── Services/            # Business logic services
    └── Helpers/             # Utility classes
```

### Frontend Structure
```
frontend/
├── index.html               # Login/signup page
├── admin/                   # Admin SPA
├── app/                     # Student app SPA
├── css/                     # Styles (variables.css for theming)
└── js/                      # JavaScript modules
    ├── api.js              # HTTP client with JWT
    ├── auth.js             # Auth logic
    ├── tenant.js           # Tenant theme loading
    ├── ui.js               # Modals, toasts
    ├── locales/           # i18n files (pt-BR, en-US, es-ES)
    ├── admin/             # Admin modules
    └── app/               # Student modules
```

---

## Multi-Tenancy

Tenant is resolved per-request in this order:
1. **Custom Domain**: `app.boxeelite.com.br` → `Tenant.CustomDomain`
2. **Subdomain**: `boxe-elite.gymapp.com` → `Tenant.Slug`
3. **Header**: `X-Tenant-Slug: boxe-elite` (development fallback)

Use `?slug=boxe-elite` query param on login page for local dev.

---

## Key Patterns

### Authentication
- JWT Bearer tokens (60 min expiry) + Refresh tokens (30 days)
- BCrypt password hashing
- Authorization via `[Authorize]` or `.RequireAuthorization()`
- Claims: `NameIdentifier` (user ID), `Email`, `Name`, `Role`

### Entity Relationships
- Tenant is the root entity
- Users, Sessions, Bookings, etc. all belong to a TenantId
- Always filter queries by TenantId in multi-tenant context

### Tenant-Aware Services
- Inject `ITenantContext` to access current tenant
- `tenantCtx.TenantId` gives the current tenant's Guid

---

## Development Seed Data

| Role | Email | Password |
|------|-------|----------|
| Super Admin | admin@gymapp.com | admin123 |
| Admin | admin@boxe-elite.com | admin123 |
| Student | joao@example.com | aluno123 |

---

## Important Notes

- **No tests configured** - no test framework installed
- **No TypeScript** - plain JavaScript only
- **No frontend bundler** - ES Modules served directly by Nginx
- **Nullable enabled** - all C# code uses nullable reference types
- Check `.agents/skills/` for available AI agent skills before major tasks

---

## Environment Variables

Required in `.env`:
```
POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD
JWT_SECRET (min 32 chars)
FRONTEND_URL
EMAIL_PROVIDER (SendGrid|Resend)
```
