# Cosmos DB Seed Script Usage Guide

## Overview
The `BF.Data.Cosmos.Seed` project seeds initial data into Azure Cosmos DB containers for development and testing.

## Configuration Flags

### Container Management

#### `DeleteContainersBeforeCreating`
**Location:** Top of `Program.cs`  
**Default:** `false`  
**Purpose:** Controls whether existing containers are deleted before being recreated

```csharp
// Container management - set to true to delete and recreate containers (WARNING: deletes all data!)
const bool DeleteContainersBeforeCreating = false;
```

**?? WARNING:** Setting this to `true` will:
- Delete ALL existing containers in the database
- Delete ALL data in those containers
- Recreate the containers with fresh schema
- This is **destructive** and **irreversible**

**When to use:**
- ? Local development with emulator
- ? Dev/test environments when you need a clean slate
- ? Testing schema migrations
- ? **NEVER in production**
- ? **NEVER with shared development databases containing important data**

### Data Seeding Control Flags

Each entity type can be individually controlled:

```csharp
const bool SeedTenants = true;           // Seed tenant documents
const bool SeedTenantConfigs = true;     // Seed tenant configuration documents
const bool SeedPayerConfigs = true;      // Seed payer configuration documents
const bool SeedPractices = true;         // Seed practice documents
const bool SeedPayers = true;            // Seed payer master documents
const bool SeedPatients = true;          // Seed patient documents
const bool SeedEncounters = true;        // Seed encounter documents
const bool SeedLookups = true;           // Seed lookup/reference data
```

**Use Cases:**
- Set all to `true`: Full database initialization
- Set specific flags to `false`: Skip certain entity types
- Useful when testing specific data without re-seeding everything

## Usage Examples

### Example 1: Fresh Start (Local Emulator)
```csharp
const string EndpointUri = "https://localhost:8081";
const bool DeleteContainersBeforeCreating = true;  // ? Safe for local emulator
// All seed flags = true
```

**Result:** Completely clean database with fresh seed data

### Example 2: Add Missing Data (Dev Environment)
```csharp
const bool DeleteContainersBeforeCreating = false;  // Don't delete existing data
const bool SeedTenants = false;                     // Already have tenants
const bool SeedPayers = false;                      // Already have payers
const bool SeedPatients = true;                     // Add new patient data
const bool SeedEncounters = true;                   // Add new encounter data
```

**Result:** Preserves existing tenants and payers, adds patients and encounters

### Example 3: Update Reference Data Only
```csharp
const bool DeleteContainersBeforeCreating = false;  // Preserve all containers
const bool SeedTenants = false;
const bool SeedPractices = false;
const bool SeedPatients = false;
const bool SeedEncounters = false;
const bool SeedLookups = true;                      // Only update lookups
```

**Result:** Updates only lookup/reference data, preserves all transactional data

### Example 4: Test Payer Schema Migration
```csharp
const bool DeleteContainersBeforeCreating = true;   // Fresh containers
const bool SeedTenants = true;
const bool SeedPayers = true;
const bool SeedPayerConfigs = true;
const bool SeedPractices = false;                   // Skip unrelated data
const bool SeedPatients = false;
const bool SeedEncounters = false;
```

**Result:** Fresh payer-related schema for testing migrations

## Container Deletion Behavior

When `DeleteContainersBeforeCreating = true`, the script:

1. **Attempts to delete each container:**
   ```
   ??  DeleteContainersBeforeCreating = true. Deleting existing containers...
     ? Deleted container: tenants
     ? Deleted container: practices
     ? Container not found (already deleted): patients
     ...
   ```

2. **Handles missing containers gracefully:**
   - If a container doesn't exist, logs a message and continues
   - No error is thrown

3. **Recreates containers with fresh schema:**
   - Uses `CreateContainerIfNotExistsAsync` after deletion
   - Ensures proper partition keys and settings

## Running the Seed Script

### From Visual Studio
1. Set `BF.Data.Cosmos.Seed` as startup project
2. Configure flags in `Program.cs`
3. Press F5 or click Run

### From Command Line
```bash
cd BF.Data.Cosmos.Seed
dotnet run
```

### Expected Output
```
Starting Cosmos seed...
??  DeleteContainersBeforeCreating = true. Deleting existing containers...
  ? Deleted container: tenants
  ? Deleted container: practices
  ...
? Container deletion complete.

Seeding tenants...
Seeding tenant configs...
Seeding payer configs...
Seeding practices...
Seeding payers...
Seeding patients...
Seeding encounters...
Seeding lookups...
Done seeding.
```

## Safety Checklist

Before setting `DeleteContainersBeforeCreating = true`, verify:

- [ ] You are **NOT** connected to a production database
- [ ] You are **NOT** connected to a shared development database with important data
- [ ] You understand that **ALL data will be permanently deleted**
- [ ] You have a backup if needed
- [ ] The endpoint URI points to the correct environment

## Troubleshooting

### Error: "Cosmos DB Error: Forbidden"
**Cause:** Insufficient permissions to delete containers  
**Solution:** Check your access key and ensure you have write permissions

### Error: "Container not found"
**Cause:** Container doesn't exist (not actually an error)  
**Solution:** This is expected behavior and will be logged as "? Container not found"

### Partial Seed Failure
**Cause:** Error during seeding of specific entity type  
**Solution:** 
1. Check the error message in console output
2. Fix the data in the builder methods
3. Set flags to only re-seed failed entities
4. Re-run script

## Best Practices

1. **Local Development:**
   - Use `DeleteContainersBeforeCreating = true` freely with the local emulator
   - Keep all seed flags `true` for consistent test data

2. **Shared Dev Environment:**
   - **Default to `DeleteContainersBeforeCreating = false`**
   - Coordinate with team before deleting shared data
   - Use specific seed flags to add incremental data

3. **Testing Migrations:**
   - Set `DeleteContainersBeforeCreating = true` in isolated test environment
   - Seed only relevant entity types
   - Verify schema changes work correctly

4. **Never in Production:**
   - **NEVER set `DeleteContainersBeforeCreating = true` in production**
   - Use proper migration scripts for production schema changes
   - Seed scripts are for development/testing only

## Related Files

- `Program.cs` - Main seed script with configuration flags
- `SCHEMA_MIGRATION_PAYERS_CONTAINER.md` - Details on payer container migration
- Entity builders (`BuildTenants()`, `BuildPayers()`, etc.) - Sample data definitions

---

**Remember:** With great power comes great responsibility. The delete flag is powerful but destructive. Use wisely! ??
