# Multi-Location Support - Implementation Plan

## Overview

Enable tenants (gyms/salons) to manage multiple physical locations (branches) under a single account, with shared students, packages, branding, and admin team.

### Architecture

```
Tenant (business account)
├── Location A (Matriz)
│   ├── Sessions (scheduled here)
│   ├── Schedules (assigned here)
│   └── Instructors (work here)
├── Location B (Filial)
│   └── ...
├── Shared: Students, Packages, ClassTypes, Admins, Branding
```

### Key Design Decisions

| Decision | Choice |
|----------|--------|
| Credits/Packages | Shared across all locations |
| Branding | Shared (same logo, colors, subdomain) |
| Location management | Tenant Admin creates/manages locations |
| Backward compatibility | All existing tenants auto-get "Matriz" location |

---

## Phase 1: Database Schema

### 1.1 New Entity: Location

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid (PK) | |
| TenantId | Guid (FK) | Links to Tenant |
| Name | string | e.g., "Matriz", "Filial Centro" |
| Address | string? | Optional |
| Phone | string? | Optional |
| IsMain | bool | Default false |
| CreatedAt | DateTime | |

### 1.2 Update Existing Entities

| Entity | Change |
|--------|---------|
| Session | Add `LocationId` (Guid, FK) |
| Schedule | Add `LocationId` (Guid, FK) |
| Instructor | Add `PrimaryLocationId?` (optional) |

### 1.3 Migration Strategy

1. Create `Locations` table
2. For each existing tenant, seed one "Matriz" location (IsMain = true)
3. Backfill all `Sessions` and `Schedules` with their tenant's Matriz location
4. `LocationId` is NOT nullable - every record has a location

```sql
-- Seed locations for existing tenants
INSERT INTO Locations (Id, TenantId, Name, IsMain, CreatedAt)
SELECT gen_random_uuid(), Id, 'Matriz', true, NOW()
FROM Tenants WHERE Id NOT IN (SELECT DISTINCT TenantId FROM Locations);

-- Backfill existing records
UPDATE Sessions 
SET LocationId = (
  SELECT Id FROM Locations 
  WHERE Locations.TenantId = Sessions.TenantId AND Locations.IsMain = true
);

UPDATE Schedules 
SET LocationId = (
  SELECT Id FROM Locations 
  WHERE Locations.TenantId = Schedules.TenantId AND Locations.IsMain = true
);
```

---

## Phase 2: Backend Changes

### 2.1 New Files

| File | Purpose |
|------|---------|
| `GymApp.Domain/Entities/Location.cs` | Location entity |
| `GymApp.Api/Endpoints/LocationEndpoints.cs` | CRUD endpoints |

### 2.2 New Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/locations` | List all locations for tenant |
| POST | `/locations` | Create new location |
| PUT | `/locations/{id}` | Update location |
| DELETE | `/locations/{id}` | Delete location (block if has future sessions) |

### 2.3 Update Existing Endpoints

| Endpoint | Change |
|----------|--------|
| `GET /sessions` | Add `locationId` query filter |
| `POST /sessions` | Require `locationId` in body |
| `GET /schedules` | Add `locationId` query filter |
| `POST /schedules` | Require `locationId` in body |
| `GET /dashboard/stats` | Add optional `locationId` param |
| `GET /dashboard/today` | Add optional `locationId` param |
| `GET /dashboard/occupancy` | Add optional `locationId` param |
| `POST /sessions/generate` | Add `locationId` filter per schedule |

### 2.4 TenantContext Enhancement

```csharp
// Add to ITenantContext and TenantContext
Guid? LocationId { get; set; }

// Resolution priority:
// 1. X-Location-Id header
// 2. Default to main location if only one exists
```

### 2.5 Validation Rules

| Rule | Behavior |
|------|----------|
| Cannot delete location with future sessions | Return 400 with session count |
| Cannot delete last location | Require at least one location |
| First location is always "Matriz" with IsMain=true | Enforced on creation |

---

## Phase 3: Admin Panel Frontend

### 3.1 New Module

| File | Purpose |
|------|---------|
| `frontend/js/admin/locations.js` | Location CRUD UI |

### 3.2 Navigation

Add "Locais" section in admin sidebar (under Settings or as top-level item).

### 3.3 Location Selector (Header)

```javascript
// In admin header, conditionally rendered
if (tenantLocations.length > 1) {
  // Show dropdown: "Todos os Locais" | "Matriz" | "Filial Centro"
  // On change: save to localStorage, update X-Location-Id header
}
```

### 3.4 Pages to Update

| Page | Changes |
|------|---------|
| Sessions | Filter by location when creating/editing |
| Schedules | Assign location when creating |
| Dashboard | Show location-aware stats, allow drill-down |
| Reports | Split metrics by location |

### 3.5 Location Management UI

- List all locations with edit/delete
- Cannot delete if location has future sessions
- Set which is "Main" (only one main allowed)
- "Add Location" form: Name, Address, Phone

---

## Phase 4: Student App

### 4.1 Location-Aware Schedule

- Default: Show classes from all locations
- Student can filter by preferred location
- "My preferred location" in profile settings

### 4.2 Booking

- Shared credits work across any location
- Booking shows which location the class is at

---

## Phase 5: Edge Cases

| Scenario | Handling |
|----------|----------|
| Delete location with future sessions | Block + show warning with session count |
| Delete last remaining location | Block - must have at least one location |
| Instructor works at multiple locations | Add `InstructorLocations` junction table |
| Student prefers specific location | `User.PreferredLocationId` (optional) |
| Package reports by location | Track `LocationId` on each `Booking` |
| First-time tenant setup | Auto-create "Matriz" location |

---

## File Changes Summary

| Layer | New Files | Modified Files |
|-------|-----------|----------------|
| Domain | `Location.cs` | - |
| Infra | - | `AppDbContext.cs`, migrations |
| API | `LocationEndpoints.cs`, `LocationDtos.cs` | `SessionEndpoints.cs`, `ScheduleEndpoints.cs`, `DashboardEndpoints.cs`, `TenantMiddleware.cs`, `SessionDtos.cs`, `ScheduleDtos.cs`, `Program.cs` |
| Frontend | `admin/locations.js` | `admin/index.html` (nav), `api.js` (location header), `admin/schedules.js` |

---

## Estimated Effort

| Phase | Complexity | Time |
|-------|------------|------|
| Phase 1 (DB) | Low | 1-2h |
| Phase 2 (Backend) | Medium | 4-6h |
| Phase 3 (Admin UI) | Medium | 4-6h |
| Phase 4 (Student App) | Low | 2h |
| Phase 5 (Edge cases) | Low | 2h |
| **Total** | | **~1.5-2 days** |

---

## Migration Checklist

- [x] Create `Location` entity
- [x] Create migration with backfill script
- [x] Implement Location CRUD endpoints
- [x] Update session/schedule endpoints with location filter
- [x] Add location header to API client
- [x] Build admin Locations management page
- [x] Add location selector to admin header
- [x] Update dashboard with location filter
- [x] Update student app with location info (Phase 4)
- [ ] Test migration on existing tenants
- [ ] Test new tenant creation (auto-Matriz)
