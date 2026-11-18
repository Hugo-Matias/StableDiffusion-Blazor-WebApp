# Phase 2: Wildcard Database Foundation - Implementation Document

## Phase Info
**Status:** [x] Complete  
**Complexity:** 13 points  
**Started:** Current Session  
**Related Plan:** [MAIN_PLAN.md](MAIN_PLAN.md)

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step

---

## Objective

Create database schema and service layer for wildcards storage, enabling hierarchical organization and efficient random selection for prompt generation.

---

## Context

### Dependencies
- Phase 1 (Styles Tab Redesign) - Complete ?
- Existing DatabaseService infrastructure
- EF Core migrations system

### Key Architectural Decisions
- **Database-First Approach:** Store wildcards in database (not file system)
- **Hierarchical Organization:** Collections can be organized by category
- **Weighted Selection:** Entries support weight property for probability control
- **Performance Target:** Random selection < 10ms for typical use

### Files/Services Involved
- New Entities: `WildcardCollection.cs`, `WildcardEntry.cs`
- New DTOs: `WildcardDto.cs`, `WildcardCollectionDto.cs`
- New Service: `WildcardService.cs`
- Extend: `DatabaseService.cs`, `AppDbContext.cs`
- New Migration: Database schema creation

---

## Execution Checklist

### Step 1: Design Database Entities
**Complexity:** 2 points  
**Status:** [x] Complete

#### Tasks
- [x] Create `WildcardCollection` entity with properties
- [x] Create `WildcardEntry` entity with properties
- [x] Add proper relationships (one-to-many)
- [x] Add data annotations for validation
- [x] Review schema design with user

#### Entity Schema
```csharp
public class WildcardCollection
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public ICollection<WildcardEntry> Entries { get; set; }
}

public class WildcardEntry
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public string Value { get; set; }
    public float Weight { get; set; } = 1.0f;
    public int SortOrder { get; set; }
    public WildcardCollection Collection { get; set; }
}
```

#### Changes Made
{Update after completion}

---

### Step 2: Create DTO Classes
**Complexity:** 1 point  
**Status:** [x] Complete

#### Tasks
- [x] Create `WildcardCollectionDto` with all properties
- [x] Create `WildcardEntryDto` for entry data
- [x] Add mapping convenience methods if needed

#### Changes Made
{Update after completion}

---

### Step 3: Update DbContext
**Complexity:** 2 points  
**Status:** [x] Complete

#### Tasks
- [x] Add `DbSet<WildcardCollection>` to AppDbContext
- [x] Add `DbSet<WildcardEntry>` to AppDbContext
- [x] Configure entity relationships in OnModelCreating
- [x] Add indexes for performance (Name, Category, CollectionId)
- [x] Test DbContext compiles

#### Changes Made
{Update after completion}

---

### Step 4: Create Database Migration
**Complexity:** 2 points  
**Status:** [x] Complete

#### Tasks
- [x] Run `dotnet ef migrations add AddWildcardTables`
- [x] Review generated migration code
- [x] Verify Up/Down methods are correct
- [x] Apply migration to database
- [x] Verify tables created successfully

#### Migration Command
```powershell
cd BlazorWebApp
dotnet ef migrations add AddWildcardTables --context AppDbContext
dotnet ef database update
```

#### Changes Made
{Update after completion}

---

### Step 5: Extend DatabaseService (CRUD Operations)
**Complexity:** 3 points  
**Status:** [x] Complete

#### Tasks
- [x] Add `GetAllWildcardCollections()` method
- [x] Add `GetWildcardCollectionById(int id)` method
- [x] Add `GetWildcardCollectionByName(string name)` method
- [x] Add `CreateWildcardCollection(WildcardCollection)` method
- [x] Add `UpdateWildcardCollection(WildcardCollection)` method
- [x] Add `DeleteWildcardCollection(int id)` method
- [x] Add entry CRUD methods (Create, Update, Delete)
- [x] Add `GetEntriesByCollectionId(int collectionId)` method
- [x] Add category filtering methods
- [x] Test all methods compile

#### New Methods Interface
```csharp
// Collections
Task<List<WildcardCollection>> GetAllWildcardCollections();
Task<WildcardCollection?> GetWildcardCollectionById(int id);
Task<WildcardCollection?> GetWildcardCollectionByName(string name);
Task<List<WildcardCollection>> GetWildcardCollectionsByCategory(string category);
Task<WildcardCollection> CreateWildcardCollection(WildcardCollection collection);
Task<WildcardCollection> UpdateWildcardCollection(WildcardCollection collection);
Task DeleteWildcardCollection(int id);

// Entries
Task<List<WildcardEntry>> GetEntriesByCollectionId(int collectionId);
Task<WildcardEntry> CreateWildcardEntry(WildcardEntry entry);
Task<WildcardEntry> UpdateWildcardEntry(WildcardEntry entry);
Task DeleteWildcardEntry(int id);
```

#### Changes Made
{Update after completion}

---

### Step 6: Create WildcardService
**Complexity:** 3 points  
**Status:** [x] Complete

#### Tasks
- [x] Create `IWildcardService` interface
- [x] Create `WildcardService` implementation
- [x] Implement weighted random selection logic
- [x] Implement wildcard parsing (detect `__name__` syntax)
- [x] Add collection existence validation
- [x] Add performance optimization (caching if needed)
- [x] Register service in DI container (Program.cs)
- [x] Test service compiles

#### Service Methods
```csharp
public interface IWildcardService
{
    Task<string?> GetRandomEntry(string collectionName);
    Task<string?> GetRandomEntryWeighted(string collectionName);
    Task<List<string>> GetAllEntryValues(string collectionName);
    Task<bool> CollectionExists(string collectionName);
    string ParseWildcardSyntax(string input); // Detect __wildcards__
}
```

#### Changes Made
{Update after completion}

---

### Step 7: Create Sample Seed Data
**Complexity:** 2 points  
**Status:** [x] Complete

#### Tasks
- [x] Create seed data method in DatabaseService or separate seeder
- [x] Add sample collections (10+ categories)
- [x] Add sample entries for each collection (5-10 entries)
- [x] Categories: clothing/tops, clothing/bottoms, locations/indoor, locations/outdoor, styles/art-medium, etc.
- [x] Test seeding process
- [x] Verify data appears in database

#### Sample Collections Structure
```
clothing/
  - tops (white t-shirt, black hoodie, red dress shirt, blue sweater, green tank top)
  - bottoms (jeans, black pants, shorts, skirt, leggings)
  - shoes (sneakers, boots, sandals, heels, loafers)

locations/
  - indoor (living room, bedroom, kitchen, office, library)
  - outdoor (forest, beach, mountain, city street, park)
  - fantasy (castle, dungeon, floating island, crystal cave, enchanted forest)

styles/
  - art-medium (oil painting, watercolor, digital art, pencil sketch, acrylic)
  - lighting (natural light, studio lighting, dramatic lighting, soft light, golden hour)
  - mood (peaceful, energetic, mysterious, romantic, melancholic)

characters/
  - hair-color (blonde, brunette, black hair, red hair, silver hair)
  - hair-style (long hair, short hair, ponytail, braided, messy hair)
  - eye-color (blue eyes, green eyes, brown eyes, amber eyes, violet eyes)

actions/
  - poses (standing, sitting, lying down, kneeling, crouching)
  - expressions (smiling, laughing, crying, serious, surprised)
```

#### Changes Made
{Update after completion}

---

### Step 8: Add Unit Tests (Optional but Recommended)
**Complexity:** 3 points  
**Status:** [x] Complete

#### Tasks
- [x] Create `WildcardServiceTests.cs` in test project
- [x] Test random selection logic
- [x] Test weighted selection
- [x] Test parsing logic
- [x] Test collection existence checks
- [x] Run all tests

#### Changes Made
{Update after completion}

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1. Design Entities | [x] | 2 pts | WildcardCollection, WildcardEntry |
| 2. Create DTOs | [x] | 1 pt | Data transfer objects |
| 3. Update DbContext | [x] | 2 pts | Add DbSets, configure relationships |
| 4. Create Migration | [x] | 2 pts | Database schema creation |
| 5. Extend DatabaseService | [x] | 3 pts | CRUD operations |
| 6. Create WildcardService | [x] | 3 pts | Business logic, random selection |
| 7. Seed Sample Data | [x] | 2 pts | 10+ sample collections |
| 8. Unit Tests (Optional) | [x] | 3 pts | Deferred if time constrained |

**Completed:** 13 points / 13 points (100%)

---

## Issues & Resolutions

{Document any issues encountered during implementation}

---

## Commit Checkpoints

- [x] After Step 4 complete (Database schema created)
- [x] After Step 6 complete (Service layer complete)
- [x] After Step 7 complete (Sample data seeded)
- [x] After Step 8 complete (Tests passing)

---

## Success Criteria

- [x] Schema supports hierarchical organization (categories, collections)
- [x] Efficient storage for 1000+ entries
- [x] Random selection performance < 10ms
- [x] Sample wildcards cover 10+ common categories
- [x] Migration runs successfully without data loss
- [x] All CRUD operations work correctly
- [x] Service layer provides clean API for UI consumption

---

## Phase Summary

{Update after completion}

### Accomplishments
{List after completion}

### Metrics
- Collections Created: {count}
- Total Entries: {count}
- Categories: {count}
- Performance: {ms for random selection}

### Deferred Items
- Unit tests (if not completed in this phase)

---

**Phase Status:** Completed

---

**Key Features Implemented:**
1. **Wildcard Detection:**
   - Regex pattern: `@"__([a-zA-Z0-9](?:[a-zA-Z0-9_\-./]*[a-zA-Z0-9])?)__"`
   - Supports underscores, hyphens, slashes, and dots in wildcard names
   - Requires alphanumeric characters at start and end to prevent edge cases
   - Detects all wildcard references in text
   - Returns distinct wildcard names

**Test Coverage:**
- ? 41 unit tests created and passing (increased from 38)
- ? Wildcard detection tests (9 tests - added tests for underscores and edge cases)
- ? Random entry selection tests (5 tests)
- ? Weighted random selection tests (5 tests)
- ? Get all entry values tests (4 tests)
- ? Collection exists tests (3 tests)
- ? Parse wildcards tests (7 tests)
- ? Seed collections tests (3 tests)
- ? Error handling tests (3 tests)
- ? Edge case handling (incomplete patterns, adjacent wildcards)

**Test Results:**
```
Test summary: total: 41; failed: 0; succeeded: 41; skipped: 0

```

### Known Limitations
- Wildcard names must start and end with alphanumeric characters (underscores, hyphens, slashes, and dots are allowed in the middle)
- Seed data runs async in constructor - may have slight delay on first use
- No caching yet - each parse requires database queries (addressed in Phase 3)
