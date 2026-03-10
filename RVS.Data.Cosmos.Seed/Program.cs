// BF.Data.Cosmos.Seed - Refactored for embedded encounters in Patient documents
// Partition Key: /practiceId for HIPAA-aligned practice isolation

using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using RVS.Domain.Entities;
using Newtonsoft.Json;
using RVS.Domain.Shared;
using System.Collections.ObjectModel;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .Build();

var EndpointUri = configuration["CosmosDb:Endpoint"] ?? throw new InvalidOperationException("CosmosDb:Endpoint is not configured.");
var Key = configuration["CosmosDb:Key"] ?? throw new InvalidOperationException("CosmosDb:Key is not configured.");
var DatabaseId = configuration["CosmosDb:DatabaseId"] ?? throw new InvalidOperationException("CosmosDb:DatabaseId is not configured.");

const string TenantsContainerId = "tenants";
const string PracticesContainerId = "practices";
const string PatientsContainerId = "patients";
const string PayersContainerId = "payers";
const string LookupsContainerId = "lookups";

// Container management - set to true to delete and recreate containers (WARNING: deletes all data!)
const bool DeleteContainersBeforeCreating = true;

// Seed control flags - set to false to skip seeding specific entity types
const bool SeedTenants = true;
const bool SeedTenantConfigs = true;
const bool SeedPayerConfigs = true;
const bool SeedPractices = true;
const bool SeedPayers = true;
const bool SeedPatients = true;  // Now includes embedded encounters
const bool SeedLookups = true;



Console.WriteLine("Starting Cosmos seed (Embedded Encounters Model)...");
Console.WriteLine("📋 Data Model: Encounters embedded in Patient documents");
Console.WriteLine("📋 Partition Key: /practiceId for patients container\n");

using var client = new CosmosClient(EndpointUri, Key);

try
{
    // 1. Ensure database & containers exist
    var database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);

    // Optionally delete containers before recreating
    if (DeleteContainersBeforeCreating)
    {
        Console.WriteLine("⚠️  DeleteContainersBeforeCreating = true. Deleting existing containers...");

        await TryDeleteContainerAsync(database.Database, TenantsContainerId);
        await TryDeleteContainerAsync(database.Database, PracticesContainerId);
        await TryDeleteContainerAsync(database.Database, PatientsContainerId);
        await TryDeleteContainerAsync(database.Database, PayersContainerId);
        await TryDeleteContainerAsync(database.Database, LookupsContainerId);

        // Add 1 second pause
        await Task.Delay(1000);

        Console.WriteLine("✓ Container deletion complete.\n");
    }

    Console.WriteLine("Creating containers with optimized indexing policies...\n");

    // Tenants container: holds tenants + configs via Type discriminator
    var tenantsContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = TenantsContainerId,
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/_etag/?" } },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/type", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/status", Order = CompositePathSortOrder.Ascending }
                    },
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/type", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/name", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        });
    Console.WriteLine("✓ Created tenants container");

    // Practices: partitioned by tenantId
    var practicesContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = PracticesContainerId,
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/_etag/?" } },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/isEnabled", Order = CompositePathSortOrder.Descending },
                        new CompositePath { Path = "/name", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        });
    Console.WriteLine("✓ Created practices container");

    // =========================================================================
    // PATIENTS CONTAINER - NEW DESIGN
    // =========================================================================
    // Partition Key: /practiceId (CHANGED from /tenantId)
    // - HIPAA-aligned physical isolation by practice
    // - All patient + encounter operations are practice-scoped
    // - Encounters embedded in patient documents (~8 avg, max ~20)
    // - Document size: 124KB avg, 304KB max (well under 2MB limit)
    // =========================================================================
    var patientsContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = PatientsContainerId,
            PartitionKeyPath = "/practiceId",  // CHANGED: practiceId for HIPAA isolation
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/_etag/?" },
                    // Exclude large nested eligibility check payloads (X12 data)
                    new ExcludedPath { Path = "/encounters/[]/eligibilityChecks/[]/payloads/*" },
                    // Exclude detailed coverage line info
                    new ExcludedPath { Path = "/encounters/[]/eligibilityChecks/[]/coverageLines/[]/additionalInfo/?" }
                },
                CompositeIndexes =
                {
                    // Patient search by last name, first name (within practice partition)
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/tenantId", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/lastName", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/firstName", Order = CompositePathSortOrder.Ascending }
                    },
                    // Patient queries by tenant and date of birth
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/tenantId", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/dateOfBirth", Order = CompositePathSortOrder.Ascending }
                    },
                    // Recent patients ordering
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/tenantId", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/updatedAtUtc", Order = CompositePathSortOrder.Descending }
                    },
                    // Patient search ORDER BY for pagination - required for ORDER BY c.lastName, c.firstName, c.id
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/lastName", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/firstName", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        });
    Console.WriteLine("✓ Created patients container (PK: /practiceId, with embedded encounters)");

    // Payers: partitioned by tenantId (GLOBAL for shared payers)
    var payersContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = PayersContainerId,
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/_etag/?" } },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/supportedPlanTypes/[]", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/name", Order = CompositePathSortOrder.Ascending }
                    },
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/type", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/isEnabled", Order = CompositePathSortOrder.Descending }
                    }
                }
            }
        });
    Console.WriteLine("✓ Created payers container");

    // Lookups: partitioned by tenantId (GLOBAL + per-tenant)
    var lookupsContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = LookupsContainerId,
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/_etag/?" } },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/category", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/name", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        });
    Console.WriteLine("✓ Created lookups container\n");

    // 2. Build sample data
    var tenants = BuildTenants();
    var tenantConfigs = BuildTenantConfigs();
    var payerConfigs = BuildPayerConfigs();
    var practices = BuildPractices();
    var payers = BuildPayers();
    var patients = BuildPatientsWithEmbeddedEncounters();  // NEW: Includes embedded encounters
    var lookupDocs = BuildLookups();

    // 3. Seed data
    if (SeedTenants)
    {
        Console.WriteLine("Seeding tenants...");
        foreach (var t in tenants)
            await tenantsContainer.Container.UpsertItemAsync(t, new PartitionKey(t.TenantId));
    }

    if (SeedTenantConfigs)
    {
        Console.WriteLine("Seeding tenant configs...");
        foreach (var cfg in tenantConfigs)
            await tenantsContainer.Container.UpsertItemAsync(cfg, new PartitionKey(cfg.TenantId));
    }

    if (SeedPayerConfigs)
    {
        Console.WriteLine("Seeding payer configs...");
        foreach (var pcfg in payerConfigs)
            await payersContainer.Container.UpsertItemAsync(pcfg, new PartitionKey(pcfg.TenantId));
    }

    if (SeedPractices)
    {
        Console.WriteLine("Seeding practices...");
        foreach (var p in practices)
            await practicesContainer.Container.UpsertItemAsync(p, new PartitionKey(p.TenantId));
    }

    if (SeedPayers)
    {
        Console.WriteLine("Seeding payers...");
        foreach (var p in payers)
        {
            var partitionKeyValue = p.TenantId ?? "GLOBAL";
            await payersContainer.Container.UpsertItemAsync(p, new PartitionKey(partitionKeyValue));
        }
    }

    if (SeedPatients)
    {
        Console.WriteLine("Seeding patients (with embedded encounters)...");
        foreach (var p in patients)
        {
            // NEW: Use practiceId as partition key
            await patientsContainer.Container.UpsertItemAsync(p, new PartitionKey(p.PracticeId));
        }
    }

    if (SeedLookups)
    {
        Console.WriteLine("Seeding lookups...");
        foreach (var l in lookupDocs)
            await lookupsContainer.Container.UpsertItemAsync(l, new PartitionKey(l.TenantId));
    }

    Console.WriteLine("\n✓ Done seeding.");
    Console.WriteLine("\n📊 Container Summary:");
    Console.WriteLine("  • Tenants: PK=/tenantId (tenant + tenantConfig documents)");
    Console.WriteLine("  • Practices: PK=/tenantId");
    Console.WriteLine("  • Patients: PK=/practiceId (HIPAA isolation) with embedded encounters");
    Console.WriteLine("  • Payers: PK=/tenantId (GLOBAL for shared payers)");
    Console.WriteLine("  • Lookups: PK=/tenantId");
    Console.WriteLine("\n📋 Data Model Benefits:");
    Console.WriteLine("  • Patient encounter history: Point read (1 RU) vs cross-partition query");
    Console.WriteLine("  • Transactional consistency across patient + coverage + encounters");
    Console.WriteLine("  • HIPAA-aligned physical isolation by practice");
}
catch (CosmosException ex)
{
    Console.WriteLine($"✗ Cosmos DB Error: {ex.StatusCode} - {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}

// Helper method to safely delete a container if it exists
static async Task TryDeleteContainerAsync(Database database, string containerId)
{
    try
    {
        var container = database.GetContainer(containerId);
        await container.DeleteContainerAsync();
        Console.WriteLine($"  ✓ Deleted container: {containerId}");
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine($"  ○ Container not found: {containerId}");
    }
}

#region Builders - Tenants + Configs

static List<Tenant> BuildTenants() =>
    new()
    {
        new Tenant { Id = "ten_001", Name = "VisionCare Partners Network", BillingEmail = "billing@visioncarepartners.com", Status = "Active", Plan = "Enterprise", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_002", Name = "Acme Eye Group", BillingEmail = "admin@acmeeye.com", Status = "Active", Plan = "Professional", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_003", Name = "Dr. Smith Family Optometry", BillingEmail = "drsmith@familyeye.net", Status = "Active", Plan = "Starter", CreatedByUserId = "seed-process" },
    };

static List<TenantConfig> BuildTenantConfigs() =>
    new()
    {
        new TenantConfig
        {
            Id = "ten_001_config",
            TenantId = "ten_001",
            CreatedByUserId = "seed-process",
            Practice = new PracticeSettings { DefaultPracticeId = "prac_001" },
            Encounters = new EncounterSettings
            {
                DefaultRoutineEncounterTypeCode = "ROUTINE_EXAM",
                DefaultMedicalEncounterTypeCode = "MEDICAL_EYE",
                EncounterTypes = new List<EncounterTypeConfig>
                {
                    new EncounterTypeConfig { Code = "ROUTINE_EXAM", DisplayName = "Routine Eye Exam", IsRoutineVision = true, DefaultCoverageType = "Vision" },
                    new EncounterTypeConfig { Code = "MEDICAL_EYE", DisplayName = "Medical Eye Visit", IsMedical = true, DefaultCoverageType = "Medical" },
                }
            },
            Eligibility = new EligibilitySettings { EnableEligibilityChecks = true, PrimaryClearinghouseCode = "AVAILITY" },
            Cob = new CobSettings { RoutineExamPriority = "VisionThenMedical", MedicalVisitPriority = "MedicalThenVision" },
            Ui = new UiSettings { ShowCoverageTab = true, ShowEncountersTab = true }
        },
        new TenantConfig
        {
            Id = "ten_002_config",
            TenantId = "ten_002",
            CreatedByUserId = "seed-process",
            Practice = new PracticeSettings { DefaultPracticeId = "prac_002" },
            Encounters = new EncounterSettings { DefaultRoutineEncounterTypeCode = "ROUTINE_EXAM" },
            Eligibility = new EligibilitySettings { EnableEligibilityChecks = true }
        },
        new TenantConfig
        {
            Id = "ten_003_config",
            TenantId = "ten_003",
            CreatedByUserId = "seed-process",
            Practice = new PracticeSettings { DefaultPracticeId = "prac_003" },
            Encounters = new EncounterSettings { DefaultRoutineEncounterTypeCode = "ROUTINE_EXAM" },
            Eligibility = new EligibilitySettings { EnableEligibilityChecks = true }
        }
    };

static List<PayerConfig> BuildPayerConfigs() =>
    new()
    {
        new PayerConfig { Id = "ten_001_prac_001_vsp", TenantId = "ten_001", PayerId = "payer_vsp_001", PracticeId = "prac_001", IsEnabled = true, SortOrder = 10, CreatedByUserId = "seed-process" },
        new PayerConfig { Id = "ten_001_prac_001_bcbs", TenantId = "ten_001", PayerId = "payer_bcbs_001", PracticeId = "prac_001", IsEnabled = true, SortOrder = 20, CreatedByUserId = "seed-process" },
        new PayerConfig { Id = "ten_002_prac_002_vsp", TenantId = "ten_002", PayerId = "payer_vsp_001", PracticeId = "prac_002", IsEnabled = true, SortOrder = 10, CreatedByUserId = "seed-process" },
        new PayerConfig { Id = "ten_003_prac_003_medicare", TenantId = "ten_003", PayerId = "payer_medicare_001", PracticeId = "prac_003", IsEnabled = true, SortOrder = 10, CreatedByUserId = "seed-process" },
    };

#endregion

#region Builders - Payers

static List<Payer> BuildPayers() =>
    new()
    {
        new Payer { Id = "payer_vsp_001", TenantId = "GLOBAL", Name = "VSP (Vision Service Plan)", SupportedPlanTypes = new List<string> { "Vision" }, AvailityPayerCode = "VSP", X12PayerId = "12345", CreatedByUserId = "seed-process" },
        new Payer { Id = "payer_bcbs_001", TenantId = "GLOBAL", Name = "Blue Cross Blue Shield", SupportedPlanTypes = new List<string> { "Medical" }, AvailityPayerCode = "BCBS", X12PayerId = "00590", CreatedByUserId = "seed-process" },
        new Payer { Id = "payer_eyemed_001", TenantId = "GLOBAL", Name = "EyeMed Vision Care", SupportedPlanTypes = new List<string> { "Vision" }, AvailityPayerCode = "EYEMED", X12PayerId = "13551", CreatedByUserId = "seed-process" },
        new Payer { Id = "payer_medicare_001", TenantId = "GLOBAL", Name = "Medicare Part B", SupportedPlanTypes = new List<string> { "Medical" }, AvailityPayerCode = "MEDICARE", X12PayerId = "00901", IsMedicare = true, CreatedByUserId = "seed-process" },
    };

#endregion

#region Builders - Practices

static List<Practice> BuildPractices() =>
    new()
    {
        new Practice
        {
            Id = "prac_001",
            TenantId = "ten_001",
            Name = "VisionCare Partners - Downtown",
            IsEnabled = true,
            Phone = "555-100-2000",
            Email = "downtown@visioncarepartners.com",
            CreatedByUserId = "seed-process",
            Locations = new List<Location>
            {
                new Location { Id = "loc_001", Name = "Main Street Clinic", Address1 = "123 Main Street", City = "Los Angeles", State = "CA", PostalCode = "90001", IsEnabled = true },
                new Location { Id = "loc_002", Name = "Financial District", Address1 = "789 Corporate Plaza", City = "Los Angeles", State = "CA", PostalCode = "90071", IsEnabled = true }
            }
        },
        new Practice
        {
            Id = "prac_002",
            TenantId = "ten_002",
            Name = "Acme Eye - Pasadena",
            IsEnabled = true,
            Phone = "555-200-3000",
            Email = "pasadena@acmeeye.com",
            CreatedByUserId = "seed-process",
            Locations = new List<Location>
            {
                new Location { Id = "loc_101", Name = "Pasadena Eye Center", Address1 = "456 Colorado Blvd", City = "Pasadena", State = "CA", PostalCode = "91101", IsEnabled = true }
            }
        },
        new Practice
        {
            Id = "prac_003",
            TenantId = "ten_003",
            Name = "Dr. Smith Family Optometry",
            IsEnabled = true,
            Phone = "555-300-4000",
            Email = "office@familyeye.net",
            CreatedByUserId = "seed-process",
            Locations = new List<Location>
            {
                new Location { Id = "loc_201", Name = "Smith Family Eye Care", Address1 = "1010 Maple Drive", City = "Glendale", State = "CA", PostalCode = "91202", IsEnabled = true }
            }
        },
        new Practice
        {
            Id = "prac_004",
            TenantId = "ten_001",
            Name = "VisionCare Partners - Costco",
            IsEnabled = true,
            Phone = "555-400-5000",
            Email = "costco@visioncarepartners.com",
            CreatedByUserId = "seed-process",
            Locations = new List<Location>
            {
                new Location { Id = "loc_301", Name = "Costco Burbank", Address1 = "1051 W Burbank Blvd", City = "Burbank", State = "CA", PostalCode = "91506", IsEnabled = true }
            }
        }
    };

#endregion

#region Builders - Patients with Embedded Encounters

/// <summary>
/// Builds patients with embedded encounters (new aggregate model).
/// Each patient document contains:
/// - Demographics
/// - CoverageEnrollments[] (Level 1 embedding)
/// - Encounters[] (Level 2 embedding)
///   - CoverageDecision (embedded object)
///   - EligibilityChecks[] (Level 3 embedding)
/// </summary>
static List<Patient> BuildPatientsWithEmbeddedEncounters() =>
    new()
    {
        // Patient 1: Emily Rodriguez - Dual coverage with embedded encounters
        new Patient
        {
            Id = "pat_001",
            TenantId = "ten_001",
            PracticeId = "prac_001",
            FirstName = "Emily",
            LastName = "Rodriguez",
            DateOfBirth = new DateOnly(1985, 3, 15),
            Email = "emily.rodriguez@email.com",
            Phone = "555-111-2222",
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>
            {
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_001_vision",
                    PayerId = "payer_vsp_001",
                    PlanType = "Vision",
                    MemberId = "VSP87654321",
                    GroupNumber = "GRP-TECH-2024",
                    RelationshipToSubscriber = "Self",
                    IsEnabled = true,
                    CobPriorityHint = 1,
                    EffectiveDate = new DateOnly(2024, 1, 1),
                    CreatedByUserId = "seed-process"
                },
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_001_medical",
                    PayerId = "payer_bcbs_001",
                    PlanType = "Medical",
                    MemberId = "BCBS-XYZ123456789",
                    GroupNumber = "TECHSOL-GRP-001",
                    RelationshipToSubscriber = "Self",
                    IsEnabled = true,
                    CobPriorityHint = 2,
                    EffectiveDate = new DateOnly(2024, 1, 1),
                    CreatedByUserId = "seed-process"
                }
            },
            // EMBEDDED ENCOUNTS (Level 2)
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_001",
                    LocationId = "loc_001",
                    VisitDate = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                    VisitType = "RoutineVision",
                    Status = "completed",
                    ExternalRef = "APPT-2025-0115-001",
                    CoverageDecision = new CoverageDecisionEmbedded
                    {
                        PrimaryCoverageEnrollmentId = "cov_001_vision",
                        SecondaryCoverageEnrollmentId = "cov_001_medical",
                        CobReason = "RoutineVision_UseVisionPlanPrimary",
                        CobDeterminationSource = "AUTO",
                        CreatedAtUtc = new DateTime(2025, 1, 15, 9, 45, 0, DateTimeKind.Utc),
                        CreatedByUserId = "seed-process"
                    },
                    EligibilityChecks = new List<EligibilityCheckEmbedded>
                    {
                        new EligibilityCheckEmbedded
                        {
                            EligibilityCheckId = "elig_001",
                            CoverageEnrollmentId = "cov_001_vision",
                            PayerId = "payer_vsp_001",
                            DateOfService = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                            RequestedAtUtc = new DateTime(2025, 1, 15, 9, 46, 0, DateTimeKind.Utc),
                            CompletedAtUtc = new DateTime(2025, 1, 15, 9, 46, 3, DateTimeKind.Utc),
                            Status = "Succeeded",
                            RawStatusCode = "1",
                            RawStatusDescription = "Active Coverage",
                            MemberIdSnapshot = "VSP87654321",
                            GroupNumberSnapshot = "GRP-TECH-2024",
                            PlanNameSnapshot = "VSP Choice Network",
                            CreatedByUserId = "seed-process",
                            CoverageLines = new List<CoverageLineEmbedded>
                            {
                                new CoverageLineEmbedded { ServiceTypeCode = "47", CoverageDescription = "Routine Vision Exam", CopayAmount = 10m, NetworkIndicator = "IN", CreatedByUserId = "seed-process" },
                                new CoverageLineEmbedded { ServiceTypeCode = "47", CoverageDescription = "Frames", AllowanceAmount = 150m, NetworkIndicator = "IN", CreatedByUserId = "seed-process" }
                            }
                        }
                    },
                    CreatedAtUtc = new DateTime(2025, 1, 15, 9, 30, 0, DateTimeKind.Utc),
                    CreatedByUserId = "seed-process"
                },
                new EncounterEmbedded
                {
                    Id = "enc_005",
                    LocationId = "loc_001",
                    VisitDate = new DateTime(2025, 1, 18, 13, 0, 0, DateTimeKind.Utc),
                    VisitType = "ContactLensFitting",
                    Status = "completed",
                    ExternalRef = "CL-FIT-2025-0118-003",
                    CoverageDecision = new CoverageDecisionEmbedded
                    {
                        PrimaryCoverageEnrollmentId = "cov_001_vision",
                        CobReason = "ContactLens_UseVisionPlanPrimary",
                        OverriddenByUser = true,
                        OverrideNote = "Patient requested VSP for contact lens benefit",
                        CreatedAtUtc = new DateTime(2025, 1, 18, 12, 50, 0, DateTimeKind.Utc),
                        CreatedByUserId = "seed-process"
                    },
                    EligibilityChecks = new List<EligibilityCheckEmbedded>
                    {
                        new EligibilityCheckEmbedded
                        {
                            EligibilityCheckId = "elig_005a",
                            CoverageEnrollmentId = "cov_001_vision",
                            PayerId = "payer_vsp_001",
                            DateOfService = new DateTime(2025, 1, 18, 0, 0, 0, DateTimeKind.Utc),
                            Status = "Succeeded",
                            MemberIdSnapshot = "VSP87654321",
                            CreatedByUserId = "seed-process",
                            CoverageLines = new List<CoverageLineEmbedded>
                            {
                                new CoverageLineEmbedded { ServiceTypeCode = "47", CoverageDescription = "Contact Lens Fitting", CopayAmount = 60m, CreatedByUserId = "seed-process" }
                            }
                        }
                    },
                    CreatedAtUtc = new DateTime(2025, 1, 18, 12, 45, 0, DateTimeKind.Utc),
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // Patient 2: Oliver Chen - Pediatric in different tenant (ten_002)
        new Patient
        {
            Id = "pat_002",
            TenantId = "ten_002",
            PracticeId = "prac_002",
            FirstName = "Oliver",
            LastName = "Chen",
            DateOfBirth = new DateOnly(2015, 7, 22),
            Phone = "555-222-3333",
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>
            {
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_002_vision",
                    PayerId = "payer_eyemed_001",
                    PlanType = "Vision",
                    MemberId = "EYEMED-789456123",
                    RelationshipToSubscriber = "Child",
                    SubscriberFirstName = "David",
                    SubscriberLastName = "Chen",
                    IsEnabled = true,
                    CobPriorityHint = 1,
                    CreatedByUserId = "seed-process"
                }
            },
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_003",
                    LocationId = "loc_101",
                    VisitDate = new DateTime(2025, 1, 12, 11, 0, 0, DateTimeKind.Utc),
                    VisitType = "RoutineVision",
                    Status = "completed",
                    CoverageDecision = new CoverageDecisionEmbedded
                    {
                        PrimaryCoverageEnrollmentId = "cov_002_vision",
                        CobReason = "SingleCoverage_PrimaryVision",
                        CreatedByUserId = "seed-process"
                    },
                    EligibilityChecks = new List<EligibilityCheckEmbedded>
                    {
                        new EligibilityCheckEmbedded
                        {
                            EligibilityCheckId = "elig_003",
                            CoverageEnrollmentId = "cov_002_vision",
                            PayerId = "payer_eyemed_001",
                            Status = "Succeeded",
                            MemberIdSnapshot = "EYEMED-789456123",
                            CreatedByUserId = "seed-process"
                        }
                    },
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // Patient 3: Margaret Williams - Medicare patient (ten_003)
        new Patient
        {
            Id = "pat_003",
            TenantId = "ten_003",
            PracticeId = "prac_003",
            FirstName = "Margaret",
            LastName = "Williams",
            DateOfBirth = new DateOnly(1952, 4, 30),
            Email = "margaret.w@seniornet.org",
            Phone = "555-333-4444",
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>
            {
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_003_medicare",
                    PayerId = "payer_medicare_001",
                    PlanType = "Medical",
                    MemberId = "1EG4-TE5-MK72",
                    RelationshipToSubscriber = "Self",
                    IsEnabled = true,
                    CobPriorityHint = 1,
                    IsCobLocked = true,
                    CobNotes = "Medicare Part B primary",
                    CreatedByUserId = "seed-process"
                },
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_003_supplemental",
                    PayerId = "payer_bcbs_001",
                    PlanType = "Medical",
                    MemberId = "BCBS-SUPP-987654",
                    GroupNumber = "MEDIGAP-PLAN-G",
                    RelationshipToSubscriber = "Self",
                    IsEnabled = true,
                    CobPriorityHint = 2,
                    CobNotes = "Medigap secondary",
                    CreatedByUserId = "seed-process"
                }
            },
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_002",
                    LocationId = "loc_201",
                    VisitDate = new DateTime(2025, 1, 10, 14, 0, 0, DateTimeKind.Utc),
                    VisitType = "Medical",
                    Status = "completed",
                    ExternalRef = "MED-EXAM-2025-010",
                    CoverageDecision = new CoverageDecisionEmbedded
                    {
                        PrimaryCoverageEnrollmentId = "cov_003_medicare",
                        SecondaryCoverageEnrollmentId = "cov_003_supplemental",
                        CobReason = "Medical_UseMedicarePrimary",
                        CreatedByUserId = "seed-process"
                    },
                    EligibilityChecks = new List<EligibilityCheckEmbedded>
                    {
                        new EligibilityCheckEmbedded
                        {
                            EligibilityCheckId = "elig_002",
                            CoverageEnrollmentId = "cov_003_medicare",
                            PayerId = "payer_medicare_001",
                            Status = "Succeeded",
                            MemberIdSnapshot = "1EG4-TE5-MK72",
                            CreatedByUserId = "seed-process",
                            CoverageLines = new List<CoverageLineEmbedded>
                            {
                                new CoverageLineEmbedded { ServiceTypeCode = "30", CoverageDescription = "Medical Services", CoinsurancePercent = 20m, DeductibleAmount = 240m, CreatedByUserId = "seed-process" }
                            }
                        }
                    },
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // Patient 4: Sarah Johnson - For failed eligibility test
        new Patient
        {
            Id = "pat_004",
            TenantId = "ten_001",
            PracticeId = "prac_004",
            FirstName = "Sarah",
            LastName = "Johnson",
            DateOfBirth = new DateOnly(1990, 9, 12),
            Phone = "555-444-5555",
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>
            {
                new CoverageEnrollmentEmbedded
                {
                    CoverageEnrollmentId = "cov_004_vision",
                    PayerId = "payer_vsp_001",
                    PlanType = "Vision",
                    MemberId = "VSP-SPOUSE-445566",
                    RelationshipToSubscriber = "Spouse",
                    IsEnabled = false,  // Terminated
                    TerminationDate = new DateOnly(2024, 12, 31),
                    CreatedByUserId = "seed-process"
                }
            },
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_004",
                    LocationId = "loc_002",
                    VisitDate = new DateTime(2025, 1, 8, 15, 30, 0, DateTimeKind.Utc),
                    VisitType = "RoutineVision",
                    Status = "completed",
                    EligibilityChecks = new List<EligibilityCheckEmbedded>
                    {
                        new EligibilityCheckEmbedded
                        {
                            EligibilityCheckId = "elig_004",
                            CoverageEnrollmentId = "cov_004_vision",
                            PayerId = "payer_vsp_001",
                            Status = "Failed",
                            RawStatusCode = "6",
                            RawStatusDescription = "Inactive Coverage",
                            MemberIdSnapshot = "VSP-SPOUSE-445566",
                            ErrorMessage = "Coverage terminated 12/31/2024",
                            CreatedByUserId = "seed-process"
                        }
                    },
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // ERROR PATH TEST PATIENT: In prac_004 (same tenant as prac_001) for cross-practice tests
        new Patient
        {
            Id = "pat_wrong_practice_001",
            TenantId = "ten_001",
            PracticeId = "prac_004",  // Different practice, same tenant
            FirstName = "WrongPractice",
            LastName = "TestPatient",
            DateOfBirth = new DateOnly(1980, 1, 1),
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>(),
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_wrong_practice_004",
                    LocationId = "loc_301",
                    VisitDate = new DateTime(2025, 1, 20, 10, 0, 0, DateTimeKind.Utc),
                    VisitType = "RoutineVision",
                    Status = "scheduled",
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // Multi-coverage test patient for CRUD tests
        new Patient
        {
            Id = "pat_multi_coverage",
            TenantId = "ten_001",
            PracticeId = "prac_001",
            FirstName = "MultiCoverage",
            LastName = "TestPatient",
            DateOfBirth = new DateOnly(1985, 12, 25),
            CreatedByUserId = "seed-process",
            CoverageEnrollments = new List<CoverageEnrollmentEmbedded>
            {
                new CoverageEnrollmentEmbedded { CoverageEnrollmentId = "cov_multi_001_vision", PayerId = "payer_vsp_001", PlanType = "Vision", MemberId = "VSP-MULTI-001", IsEnabled = true, CobPriorityHint = 1, CreatedByUserId = "seed-process" },
                new CoverageEnrollmentEmbedded { CoverageEnrollmentId = "cov_multi_001_medical", PayerId = "payer_bcbs_001", PlanType = "Medical", MemberId = "BCBS-MULTI-001", IsEnabled = true, CobPriorityHint = 2, CreatedByUserId = "seed-process" }
            },
            Encounters = new List<EncounterEmbedded>
            {
                new EncounterEmbedded
                {
                    Id = "enc_minimal_data",
                    LocationId = "loc_001",
                    VisitDate = new DateTime(2025, 1, 24, 9, 0, 0, DateTimeKind.Utc),
                    VisitType = "RoutineVision",
                    Status = "scheduled",
                    CreatedByUserId = "seed-process"
                }
            }
        },

        // Edge case test:  no coverage enrollments or encounters
        new Patient
        {
            Id = "pat_no_coverage_001",
            TenantId = "ten_001",
            PracticeId = "prac_001",
            FirstName = "NoCoverage",
            LastName = "TestPatient",
            DateOfBirth = new DateOnly(1985, 12, 25),
            CreatedByUserId = "seed-process",
            CoverageEnrollments = [],
            Encounters = []
        }
    };

#endregion

#region Builders - Lookups

static List<LookupSet> BuildLookups() =>
    new()
    {
        // =====================================================
        // X12 Service Type Codes (270/271 EDI)
        // =====================================================
        new LookupSet
        {
            Id = "x12-service-types",
            TenantId = "GLOBAL",
            Category = "ServiceType",
            Name = "X12 Service Type Codes",
            Description = "Healthcare Eligibility Benefit Inquiry and Response (270/271) service type codes",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                // General Medical
                new LookupItem { Code = "1", Name = "Medical Care", Description = "General medical services", SortOrder = 10 },
                new LookupItem { Code = "2", Name = "Surgical", Description = "Surgical services", SortOrder = 20 },
                new LookupItem { Code = "3", Name = "Consultation", Description = "Medical consultation", SortOrder = 30 },
                new LookupItem { Code = "4", Name = "Diagnostic X-Ray", Description = "Diagnostic radiology", SortOrder = 40 },
                new LookupItem { Code = "5", Name = "Diagnostic Lab", Description = "Laboratory services", SortOrder = 50 },
                new LookupItem { Code = "6", Name = "Radiation Therapy", Description = "Radiation treatment", SortOrder = 60 },
                new LookupItem { Code = "7", Name = "Anesthesia", Description = "Anesthesiology services", SortOrder = 70 },
                new LookupItem { Code = "8", Name = "Surgical Assistance", Description = "Assistant surgeon services", SortOrder = 80 },
                new LookupItem { Code = "9", Name = "Other Medical", Description = "Other medical services", SortOrder = 90 },
                new LookupItem { Code = "10", Name = "Blood Charges", Description = "Blood and blood products", SortOrder = 100 },
                new LookupItem { Code = "11", Name = "Used Durable Medical Equipment", Description = "Pre-owned DME", SortOrder = 110 },
                new LookupItem { Code = "12", Name = "Durable Medical Equipment Purchase", Description = "New DME purchase", SortOrder = 120 },
                new LookupItem { Code = "13", Name = "Ambulatory Service Center Facility", Description = "Outpatient facility", SortOrder = 130 },
                new LookupItem { Code = "14", Name = "Renal Supplies in the Home", Description = "Home dialysis supplies", SortOrder = 140 },
                new LookupItem { Code = "15", Name = "Alternate Method Dialysis", Description = "Alternative dialysis methods", SortOrder = 150 },
                new LookupItem { Code = "16", Name = "Chronic Renal Disease (CRD) Equipment", Description = "CRD medical equipment", SortOrder = 160 },
                new LookupItem { Code = "17", Name = "Pre-Admission Testing", Description = "Pre-operative testing", SortOrder = 170 },
                new LookupItem { Code = "18", Name = "Durable Medical Equipment Rental", Description = "DME rental services", SortOrder = 180 },
                new LookupItem { Code = "19", Name = "Pneumonia Vaccine", Description = "Pneumonia vaccination", SortOrder = 190 },
                new LookupItem { Code = "20", Name = "Second Surgical Opinion", Description = "Independent surgical review", SortOrder = 200 },
                new LookupItem { Code = "21", Name = "Third Surgical Opinion", Description = "Additional surgical review", SortOrder = 210 },
                
                // Dental
                new LookupItem { Code = "23", Name = "Diagnostic Dental", Description = "Dental exams and diagnostics", SortOrder = 230 },
                new LookupItem { Code = "24", Name = "Periodontics", Description = "Gum disease treatment", SortOrder = 240 },
                new LookupItem { Code = "25", Name = "Restorative", Description = "Fillings, crowns, bridges", SortOrder = 250 },
                new LookupItem { Code = "26", Name = "Endodontics", Description = "Root canal therapy", SortOrder = 260 },
                new LookupItem { Code = "27", Name = "Maxillofacial Prosthetics", Description = "Facial prosthetics", SortOrder = 270 },
                new LookupItem { Code = "28", Name = "Adjunctive Dental Services", Description = "Supplemental dental services", SortOrder = 280 },
                
                // General Health Coverage
                new LookupItem { Code = "30", Name = "Health Benefit Plan Coverage", Description = "General medical coverage (default)", SortOrder = 300 },
                new LookupItem { Code = "32", Name = "Plan Waiting Period", Description = "Waiting period for coverage", SortOrder = 320 },
                new LookupItem { Code = "33", Name = "Chiropractic", Description = "Chiropractic services", SortOrder = 330 },
                new LookupItem { Code = "34", Name = "Chiropractic Office Visits", Description = "Chiropractor office visits", SortOrder = 340 },
                new LookupItem { Code = "35", Name = "Dental Care", Description = "General dental services", SortOrder = 350 },
                new LookupItem { Code = "36", Name = "Dental Crowns", Description = "Crown procedures", SortOrder = 360 },
                new LookupItem { Code = "37", Name = "Dental Accident", Description = "Accident-related dental", SortOrder = 370 },
                new LookupItem { Code = "38", Name = "Orthodontics", Description = "Braces and alignment", SortOrder = 380 },
                new LookupItem { Code = "39", Name = "Prosthodontics", Description = "Dentures and prosthetics", SortOrder = 390 },
                new LookupItem { Code = "40", Name = "Oral Surgery", Description = "Dental surgery", SortOrder = 400 },
                new LookupItem { Code = "41", Name = "Routine (Preventive) Dental", Description = "Cleanings and preventive", SortOrder = 410 },
                new LookupItem { Code = "42", Name = "Home Health Care", Description = "In-home medical care", SortOrder = 420 },
                new LookupItem { Code = "43", Name = "Home Health Prescriptions", Description = "Home health medications", SortOrder = 430 },
                new LookupItem { Code = "44", Name = "Home Health Visits", Description = "Home healthcare visits", SortOrder = 440 },
                new LookupItem { Code = "45", Name = "Hospice", Description = "End-of-life care", SortOrder = 450 },
                new LookupItem { Code = "46", Name = "Respite Care", Description = "Temporary relief care", SortOrder = 460 },
                new LookupItem { Code = "47", Name = "Hospital", Description = "Inpatient hospital services", SortOrder = 470 },
                new LookupItem { Code = "48", Name = "Hospital - Inpatient", Description = "Inpatient hospitalization", SortOrder = 480 },
                new LookupItem { Code = "49", Name = "Hospital - Room and Board", Description = "Hospital room charges", SortOrder = 490 },
                new LookupItem { Code = "50", Name = "Hospital - Outpatient", Description = "Outpatient hospital services", SortOrder = 500 },
                
                // Mental Health & Substance Abuse
                new LookupItem { Code = "51", Name = "Hospital - Emergency Accident", Description = "Emergency accident care", SortOrder = 510 },
                new LookupItem { Code = "52", Name = "Hospital - Emergency Medical", Description = "Emergency medical care", SortOrder = 520 },
                new LookupItem { Code = "53", Name = "Hospital - Ambulatory Surgical", Description = "Outpatient surgery", SortOrder = 530 },
                new LookupItem { Code = "54", Name = "Long Term Care", Description = "Extended care facility", SortOrder = 540 },
                new LookupItem { Code = "55", Name = "Major Medical", Description = "Major medical coverage", SortOrder = 550 },
                new LookupItem { Code = "56", Name = "Medically Related Transportation", Description = "Medical transport services", SortOrder = 560 },
                new LookupItem { Code = "57", Name = "Air Transportation", Description = "Medical air transport", SortOrder = 570 },
                new LookupItem { Code = "58", Name = "Cabulance", Description = "Cab/ambulance service", SortOrder = 580 },
                new LookupItem { Code = "59", Name = "Licensed Ambulance", Description = "Ambulance services", SortOrder = 590 },
                new LookupItem { Code = "60", Name = "General Benefits", Description = "General benefit coverage", SortOrder = 600 },
                new LookupItem { Code = "61", Name = "In-vitro Fertilization", Description = "IVF procedures", SortOrder = 610 },
                new LookupItem { Code = "62", Name = "MRI/CAT Scan", Description = "Advanced imaging", SortOrder = 620 },
                new LookupItem { Code = "63", Name = "Donor Procedures", Description = "Organ/tissue donation", SortOrder = 630 },
                new LookupItem { Code = "64", Name = "Acupuncture", Description = "Acupuncture treatment", SortOrder = 640 },
                new LookupItem { Code = "65", Name = "Newborn Care", Description = "Neonatal services", SortOrder = 650 },
                new LookupItem { Code = "66", Name = "Pathology", Description = "Pathology services", SortOrder = 660 },
                new LookupItem { Code = "67", Name = "Smoking Cessation", Description = "Smoking cessation programs", SortOrder = 670 },
                new LookupItem { Code = "68", Name = "Well Baby Care", Description = "Pediatric wellness", SortOrder = 680 },
                new LookupItem { Code = "69", Name = "Maternity", Description = "Prenatal and delivery", SortOrder = 690 },
                new LookupItem { Code = "70", Name = "Transplants", Description = "Organ transplant services", SortOrder = 700 },
                new LookupItem { Code = "71", Name = "Audiology Exam", Description = "Hearing tests", SortOrder = 710 },
                new LookupItem { Code = "72", Name = "Inhalation Therapy", Description = "Respiratory therapy", SortOrder = 720 },
                new LookupItem { Code = "73", Name = "Diagnostic Medical", Description = "Medical diagnostics", SortOrder = 730 },
                new LookupItem { Code = "74", Name = "Private Duty Nursing", Description = "Private nursing care", SortOrder = 740 },
                new LookupItem { Code = "75", Name = "Prosthetic Device", Description = "Prosthetic limbs/devices", SortOrder = 750 },
                new LookupItem { Code = "76", Name = "Dialysis", Description = "Kidney dialysis", SortOrder = 760 },
                new LookupItem { Code = "77", Name = "Otological Exam", Description = "Ear examination", SortOrder = 770 },
                new LookupItem { Code = "78", Name = "Chemotherapy", Description = "Cancer chemotherapy", SortOrder = 780 },
                new LookupItem { Code = "79", Name = "Allergy Testing", Description = "Allergy diagnostics", SortOrder = 790 },
                new LookupItem { Code = "80", Name = "Immunizations", Description = "Vaccinations", SortOrder = 800 },
                new LookupItem { Code = "81", Name = "Routine Physical", Description = "Annual physical exam", SortOrder = 810 },
                new LookupItem { Code = "82", Name = "Family Planning", Description = "Contraception services", SortOrder = 820 },
                new LookupItem { Code = "83", Name = "Infertility", Description = "Fertility treatment", SortOrder = 830 },
                new LookupItem { Code = "84", Name = "Abortion", Description = "Abortion services", SortOrder = 840 },
                new LookupItem { Code = "85", Name = "AIDS", Description = "HIV/AIDS treatment", SortOrder = 850 },
                new LookupItem { Code = "86", Name = "Emergency Services", Description = "Emergency care", SortOrder = 860 },
                new LookupItem { Code = "87", Name = "Cancer", Description = "Cancer treatment", SortOrder = 870 },
                new LookupItem { Code = "88", Name = "Pharmacy", Description = "Prescription drugs", SortOrder = 880 },
                new LookupItem { Code = "89", Name = "Free Standing Prescription Drug", Description = "Pharmacy-only prescriptions", SortOrder = 890 },
                new LookupItem { Code = "90", Name = "Mail Order Prescription Drug", Description = "Mail-order pharmacy", SortOrder = 900 },
                new LookupItem { Code = "91", Name = "Brand Name Prescription Drug", Description = "Brand-name drugs", SortOrder = 910 },
                new LookupItem { Code = "92", Name = "Generic Prescription Drug", Description = "Generic medications", SortOrder = 920 },
                new LookupItem { Code = "93", Name = "Podiatry", Description = "Foot care services", SortOrder = 930 },
                new LookupItem { Code = "94", Name = "Podiatry - Office Visits", Description = "Podiatrist office visits", SortOrder = 940 },
                new LookupItem { Code = "95", Name = "Podiatry - Nursing Home Visits", Description = "Nursing home podiatry", SortOrder = 950 },
                new LookupItem { Code = "96", Name = "Professional (Physician)", Description = "Physician services", SortOrder = 960 },
                new LookupItem { Code = "97", Name = "Anesthesiologist", Description = "Anesthesiologist services", SortOrder = 970 },
                new LookupItem { Code = "98", Name = "Professional (Physician) Visit - Office", Description = "Doctor office visits", SortOrder = 980 },
                new LookupItem { Code = "99", Name = "Professional (Physician) Visit - Inpatient", Description = "Hospital physician visits", SortOrder = 990 },
                new LookupItem { Code = "A0", Name = "Professional (Physician) Visit - Outpatient", Description = "Outpatient physician visits", SortOrder = 1000 },
                new LookupItem { Code = "A1", Name = "Professional (Physician) Visit - Nursing Home", Description = "Nursing home physician visits", SortOrder = 1010 },
                new LookupItem { Code = "A2", Name = "Professional (Physician) Visit - Skilled Nursing Facility", Description = "SNF physician visits", SortOrder = 1020 },
                new LookupItem { Code = "A3", Name = "Professional (Physician) Visit - Home", Description = "Home physician visits", SortOrder = 1030 },
                new LookupItem { Code = "A4", Name = "Psychiatric", Description = "Mental health services", SortOrder = 1040 },
                new LookupItem { Code = "A5", Name = "Psychiatric - Room and Board", Description = "Psychiatric facility room", SortOrder = 1050 },
                new LookupItem { Code = "A6", Name = "Psychotherapy", Description = "Psychological therapy", SortOrder = 1060 },
                new LookupItem { Code = "A7", Name = "Psychiatric - Inpatient", Description = "Inpatient mental health", SortOrder = 1070 },
                new LookupItem { Code = "A8", Name = "Psychiatric - Outpatient", Description = "Outpatient mental health", SortOrder = 1080 },
                new LookupItem { Code = "A9", Name = "Rehabilitation", Description = "Rehabilitation services", SortOrder = 1090 },
                new LookupItem { Code = "AA", Name = "Rehabilitation - Room and Board", Description = "Rehab facility room", SortOrder = 1100 },
                new LookupItem { Code = "AB", Name = "Rehabilitation - Inpatient", Description = "Inpatient rehabilitation", SortOrder = 1110 },
                new LookupItem { Code = "AC", Name = "Rehabilitation - Outpatient", Description = "Outpatient rehabilitation", SortOrder = 1120 },
                new LookupItem { Code = "AD", Name = "Occupational Therapy", Description = "Occupational therapy", SortOrder = 1130 },
                new LookupItem { Code = "AE", Name = "Physical Medicine", Description = "Physical medicine services", SortOrder = 1140 },
                new LookupItem { Code = "AF", Name = "Speech Therapy", Description = "Speech therapy", SortOrder = 1150 },
                new LookupItem { Code = "AG", Name = "Skilled Nursing Care", Description = "Skilled nursing services", SortOrder = 1160 },
                new LookupItem { Code = "AH", Name = "Skilled Nursing Care - Room and Board", Description = "SNF room charges", SortOrder = 1170 },
                new LookupItem { Code = "AI", Name = "Substance Abuse", Description = "Substance abuse treatment", SortOrder = 1180 },
                new LookupItem { Code = "AJ", Name = "Alcoholism", Description = "Alcohol treatment", SortOrder = 1190 },
                new LookupItem { Code = "AK", Name = "Drug Addiction", Description = "Drug addiction treatment", SortOrder = 1200 },
                
                // Vision
                new LookupItem { Code = "AL", Name = "Vision (Optometry)", Description = "Eye exams and vision care", SortOrder = 1210 },
                new LookupItem { Code = "AM", Name = "Frames", Description = "Eyeglass frames", SortOrder = 1220 },
                new LookupItem { Code = "AN", Name = "Routine Exam", Description = "Routine eye examination", SortOrder = 1230 },
                new LookupItem { Code = "AO", Name = "Lenses", Description = "Eyeglass lenses", SortOrder = 1240 },
                new LookupItem { Code = "AQ", Name = "Nonmedically Necessary Physical", Description = "Non-required physical exam", SortOrder = 1260 },
                new LookupItem { Code = "AR", Name = "Experimental Drug Therapy", Description = "Experimental medications", SortOrder = 1270 },
                new LookupItem { Code = "B1", Name = "Burn Care", Description = "Burn treatment", SortOrder = 1280 },
                new LookupItem { Code = "B2", Name = "Brand Name Prescription Drug - Formulary", Description = "Formulary brand drugs", SortOrder = 1290 },
                new LookupItem { Code = "B3", Name = "Brand Name Prescription Drug - Non-Formulary", Description = "Non-formulary brand drugs", SortOrder = 1300 },
                new LookupItem { Code = "BA", Name = "Independent Medical Evaluation", Description = "IME services", SortOrder = 1310 },
                new LookupItem { Code = "BB", Name = "Partial Hospitalization (Psychiatric)", Description = "Partial psychiatric hospitalization", SortOrder = 1320 },
                new LookupItem { Code = "BC", Name = "Day Care (Psychiatric)", Description = "Psychiatric day programs", SortOrder = 1330 },
                new LookupItem { Code = "BD", Name = "Cognitive Therapy", Description = "Cognitive behavioral therapy", SortOrder = 1340 },
                new LookupItem { Code = "BE", Name = "Massage Therapy", Description = "Therapeutic massage", SortOrder = 1350 },
                new LookupItem { Code = "BF", Name = "Pulmonary Rehabilitation", Description = "Lung rehabilitation", SortOrder = 1360 },
                new LookupItem { Code = "BG", Name = "Cardiac Rehabilitation", Description = "Heart rehabilitation", SortOrder = 1370 },
                new LookupItem { Code = "BH", Name = "Pediatric", Description = "Pediatric services", SortOrder = 1380 },
                new LookupItem { Code = "BI", Name = "Nursery", Description = "Hospital nursery", SortOrder = 1390 },
                new LookupItem { Code = "BJ", Name = "Skin", Description = "Dermatology services", SortOrder = 1400 },
                new LookupItem { Code = "BK", Name = "Orthopedic", Description = "Orthopedic services", SortOrder = 1410 },
                new LookupItem { Code = "BL", Name = "Cardiac", Description = "Cardiac services", SortOrder = 1420 },
                new LookupItem { Code = "BM", Name = "Lymphatic", Description = "Lymphatic system services", SortOrder = 1430 },
                new LookupItem { Code = "BN", Name = "Gastrointestinal", Description = "GI services", SortOrder = 1440 },
                new LookupItem { Code = "BP", Name = "Endocrine", Description = "Endocrine services", SortOrder = 1450 },
                new LookupItem { Code = "BQ", Name = "Neurology", Description = "Neurological services", SortOrder = 1460 },
                new LookupItem { Code = "BR", Name = "Eye Care (Ophthalmology)", Description = "Medical ophthalmology services", SortOrder = 1470 },
                new LookupItem { Code = "BS", Name = "Invasive Procedures", Description = "Invasive medical procedures", SortOrder = 1480 },
                new LookupItem { Code = "BT", Name = "Gynecological", Description = "Women's health services", SortOrder = 1490 },
                new LookupItem { Code = "BU", Name = "Obstetrical", Description = "Pregnancy services", SortOrder = 1500 },
                new LookupItem { Code = "BV", Name = "Obstetrical/Gynecological", Description = "OB/GYN services", SortOrder = 1510 },
                new LookupItem { Code = "BW", Name = "Mail Order Prescription Drug: Brand Name", Description = "Mail-order brand drugs", SortOrder = 1520 },
                new LookupItem { Code = "BX", Name = "Mail Order Prescription Drug: Generic", Description = "Mail-order generic drugs", SortOrder = 1530 },
                new LookupItem { Code = "BY", Name = "Physician Visit - Office: Sick", Description = "Sick visit", SortOrder = 1540 },
                new LookupItem { Code = "BZ", Name = "Physician Visit - Office: Well", Description = "Wellness visit", SortOrder = 1550 },
                new LookupItem { Code = "C1", Name = "Coronary Care", Description = "Coronary care unit", SortOrder = 1560 },
                new LookupItem { Code = "CA", Name = "Private Duty Nursing - Inpatient", Description = "Inpatient private nursing", SortOrder = 1570 },
                new LookupItem { Code = "CB", Name = "Private Duty Nursing - Home", Description = "Home private nursing", SortOrder = 1580 },
                new LookupItem { Code = "CC", Name = "Surgical Benefits - Professional (Physician)", Description = "Surgeon fees", SortOrder = 1590 },
                new LookupItem { Code = "CD", Name = "Surgical Benefits - Facility", Description = "Surgery facility fees", SortOrder = 1600 },
                new LookupItem { Code = "CE", Name = "Mental Health Provider - Inpatient", Description = "Inpatient mental health provider", SortOrder = 1610 },
                new LookupItem { Code = "CF", Name = "Mental Health Provider - Outpatient", Description = "Outpatient mental health provider", SortOrder = 1620 },
                new LookupItem { Code = "CG", Name = "Mental Health Facility - Inpatient", Description = "Inpatient mental health facility", SortOrder = 1630 },
                new LookupItem { Code = "CH", Name = "Mental Health Facility - Outpatient", Description = "Outpatient mental health facility", SortOrder = 1640 },
                new LookupItem { Code = "CI", Name = "Substance Abuse Facility - Inpatient", Description = "Inpatient substance abuse facility", SortOrder = 1650 },
                new LookupItem { Code = "CJ", Name = "Substance Abuse Facility - Outpatient", Description = "Outpatient substance abuse facility", SortOrder = 1660 },
                new LookupItem { Code = "CK", Name = "Screening X-ray", Description = "Screening radiology", SortOrder = 1670 },
                new LookupItem { Code = "CL", Name = "Screening laboratory", Description = "Screening lab tests", SortOrder = 1680 },
                new LookupItem { Code = "CM", Name = "Mammogram, High Risk Patient", Description = "High-risk mammography", SortOrder = 1690 },
                new LookupItem { Code = "CN", Name = "Mammogram, Low Risk Patient", Description = "Low-risk mammography", SortOrder = 1700 },
                new LookupItem { Code = "CO", Name = "Flu Vaccination", Description = "Influenza vaccine", SortOrder = 1710 },
                new LookupItem { Code = "CP", Name = "Eyewear and Eyewear Accessories", Description = "Glasses and accessories", SortOrder = 1720 },
                new LookupItem { Code = "CQ", Name = "Case Management", Description = "Care coordination", SortOrder = 1730 },
                new LookupItem { Code = "DG", Name = "Dermatology", Description = "Skin care services", SortOrder = 1740 },
                new LookupItem { Code = "DM", Name = "Durable Medical Equipment", Description = "DME general", SortOrder = 1750 },
                new LookupItem { Code = "DS", Name = "Diabetic Supplies", Description = "Diabetes management supplies", SortOrder = 1760 },
                new LookupItem { Code = "GF", Name = "Generic Prescription Drug - Formulary", Description = "Formulary generic drugs", SortOrder = 1770 },
                new LookupItem { Code = "GN", Name = "Generic Prescription Drug - Non-Formulary", Description = "Non-formulary generic drugs", SortOrder = 1780 },
                new LookupItem { Code = "GY", Name = "Allergy", Description = "Allergy services", SortOrder = 1790 },
                new LookupItem { Code = "IC", Name = "Intensive Care", Description = "ICU services", SortOrder = 1800 },
                new LookupItem { Code = "MH", Name = "Mental Health", Description = "General mental health", SortOrder = 1810 },
                new LookupItem { Code = "NI", Name = "Neonatal Intensive Care", Description = "NICU services", SortOrder = 1820 },
                new LookupItem { Code = "ON", Name = "Oncology", Description = "Cancer care", SortOrder = 1830 },
                new LookupItem { Code = "PT", Name = "Physical Therapy", Description = "Physical therapy", SortOrder = 1840 },
                new LookupItem { Code = "PU", Name = "Pulmonary", Description = "Lung/respiratory care", SortOrder = 1850 },
                new LookupItem { Code = "RN", Name = "Renal", Description = "Kidney services", SortOrder = 1860 },
                new LookupItem { Code = "RT", Name = "Residential Psychiatric Treatment", Description = "Residential mental health", SortOrder = 1870 },
                new LookupItem { Code = "TC", Name = "Transitional Care", Description = "Transitional healthcare", SortOrder = 1880 },
                new LookupItem { Code = "TN", Name = "Transitional Nursery Care", Description = "Transitional neonatal care", SortOrder = 1890 },
                new LookupItem { Code = "UC", Name = "Urgent Care", Description = "Urgent care services", SortOrder = 1900 }
            }
        },
        
        // =====================================================
        // Vision & Eye Care Service Types (Curated)
        // =====================================================
        new LookupSet
        {
            Id = "vision-service-types",
            TenantId = "GLOBAL",
            Category = "VisionServiceType",
            Name = "Vision & Eye Care Service Types",
            Description = "X12 service type codes relevant to optometry and ophthalmology practices",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                // Core Vision Services
                new LookupItem { Code = "AL", Name = "Vision (Optometry)", Description = "Eye exams and vision care", SortOrder = 10 },
                new LookupItem { Code = "AN", Name = "Routine Eye Exam", Description = "Routine eye examination", SortOrder = 20 },
                new LookupItem { Code = "BR", Name = "Eye Care (Ophthalmology)", Description = "Medical ophthalmology services", SortOrder = 30 },
                
                // Eyewear
                new LookupItem { Code = "AM", Name = "Frames", Description = "Eyeglass frames", SortOrder = 40 },
                new LookupItem { Code = "AO", Name = "Lenses", Description = "Eyeglass lenses", SortOrder = 50 },
                new LookupItem { Code = "CP", Name = "Eyewear Accessories", Description = "Glasses and accessories", SortOrder = 60 },
                
                // Medical Eye Care
                new LookupItem { Code = "30", Name = "Medical Eye Services", Description = "General medical coverage for eye conditions", SortOrder = 70 },
                new LookupItem { Code = "98", Name = "Office Visit", Description = "Doctor office visits for medical eye care", SortOrder = 80 },
                new LookupItem { Code = "2", Name = "Surgical", Description = "Eye surgery procedures", SortOrder = 90 },
                
                // Diagnostics
                new LookupItem { Code = "4", Name = "Diagnostic Imaging", Description = "Diagnostic X-ray and imaging", SortOrder = 100 },
                new LookupItem { Code = "5", Name = "Laboratory", Description = "Laboratory services", SortOrder = 110 },
                new LookupItem { Code = "62", Name = "MRI/CAT Scan", Description = "Advanced imaging", SortOrder = 120 },
                new LookupItem { Code = "73", Name = "Diagnostic Medical", Description = "Medical diagnostics", SortOrder = 130 }
            }
        },
        
        // =====================================================
        // Visit Types
        // =====================================================
        new LookupSet
        {
            Id = "visit-types",
            TenantId = "GLOBAL",
            Category = "VisitType",
            Name = "Visit Types",
            Description = "Common encounter/visit types for optometry and medical practices",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                // Routine Vision Care
                new LookupItem { Code = "RoutineVision", Name = "Routine Vision Exam", Description = "Annual eye exam", SortOrder = 10 },
                new LookupItem { Code = "AnnualVision", Name = "Annual Vision Exam", Description = "Annual comprehensive vision exam", SortOrder = 15 },
                new LookupItem { Code = "ComprehensiveExam", Name = "Comprehensive Eye Exam", Description = "Detailed eye examination", SortOrder = 20 },
                
                // Medical Eye Conditions
                new LookupItem { Code = "Medical", Name = "Medical Eye Visit", Description = "Medical eye condition", SortOrder = 30 },
                new LookupItem { Code = "EmergencyEye", Name = "Emergency Eye Care", Description = "Urgent eye condition", SortOrder = 40 },
                new LookupItem { Code = "UrgentEyeCare", Name = "Urgent Eye Care", Description = "Urgent but non-emergency eye care", SortOrder = 45 },
                new LookupItem { Code = "Infection", Name = "Eye Infection", Description = "Bacterial, viral, or other eye infection", SortOrder = 50 },
                new LookupItem { Code = "Injury", Name = "Eye Injury", Description = "Traumatic eye injury", SortOrder = 55 },
                
                // Specific Medical Conditions
                new LookupItem { Code = "Glaucoma", Name = "Glaucoma Exam", Description = "Glaucoma evaluation and monitoring", SortOrder = 60 },
                new LookupItem { Code = "DiabeticEyeExam", Name = "Diabetic Eye Exam", Description = "Diabetic retinopathy screening", SortOrder = 65 },
                new LookupItem { Code = "RetinalExam", Name = "Retinal Examination", Description = "Detailed retinal examination", SortOrder = 70 },
                
                // Contact Lenses
                new LookupItem { Code = "ContactLensFitting", Name = "Contact Lens Fitting (CL Fitting)", Description = "Contact lens exam and fitting", SortOrder = 80 },
                
                // Eyewear Services
                new LookupItem { Code = "GlassesDispensing", Name = "Glasses Dispensing", Description = "Eyeglass dispensing and fitting", SortOrder = 90 },
                new LookupItem { Code = "FrameSelection", Name = "Frame Selection", Description = "Frame fitting and selection", SortOrder = 100 },
                
                // Follow-up and Special
                new LookupItem { Code = "Recheck", Name = "Recheck Visit", Description = "Follow-up examination", SortOrder = 110 },
                new LookupItem { Code = "Pediatric", Name = "Pediatric Vision Exam", Description = "Child eye exam", SortOrder = 120 }
            }
        },

        // =====================================================
        // Plan Types
        // =====================================================
        new LookupSet
        {
            Id = "plan-types",
            TenantId = "GLOBAL",
            Category = "PlanType",
            Name = "Plan Types",
            Description = "Insurance plan types for coverage enrollments",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                new LookupItem { Code = "Vision", Name = "Vision", Description = "Vision insurance plan", SortOrder = 10 },
                new LookupItem { Code = "Medical", Name = "Medical", Description = "Medical insurance plan", SortOrder = 20 },
                new LookupItem { Code = "Dental", Name = "Dental", Description = "Dental insurance plan", SortOrder = 30 },
                new LookupItem { Code = "Medicare", Name = "Medicare", Description = "Medicare coverage", SortOrder = 40 },
                new LookupItem { Code = "Medicaid", Name = "Medicaid", Description = "Medicaid coverage", SortOrder = 50 }
            }
        },

        // =====================================================
        // Relationship Types
        // =====================================================
        new LookupSet
        {
            Id = "relationship-types",
            TenantId = "GLOBAL",
            Category = "RelationshipType",
            Name = "Relationship Types",
            Description = "Relationship of patient to subscriber for insurance coverage",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                new LookupItem { Code = "Self", Name = "Self", Description = "Patient is the subscriber", SortOrder = 10 },
                new LookupItem { Code = "Spouse", Name = "Spouse", Description = "Patient is spouse of subscriber", SortOrder = 20 },
                new LookupItem { Code = "Child", Name = "Child", Description = "Patient is child of subscriber", SortOrder = 30 },
                new LookupItem { Code = "Dependent", Name = "Dependent", Description = "Patient is dependent of subscriber", SortOrder = 40 },
                new LookupItem { Code = "Other", Name = "Other", Description = "Other relationship to subscriber", SortOrder = 50 }
            }
        },

        // =====================================================
        // COB Reasons
        // =====================================================
        new LookupSet
        {
            Id = "cob-reasons",
            TenantId = "GLOBAL",
            Category = "CobReason",
            Name = "Coordination of Benefits Reasons",
            Description = "Reasons for coverage decision determination and COB priority",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                // Vision-first scenarios
                new LookupItem { Code = "RoutineVision_UseVisionPlanPrimary", Name = "Routine Vision - Vision Plan Primary", Description = "Routine eye exam using vision plan as primary", SortOrder = 10 },
                new LookupItem { Code = "ContactLens_UseVisionPlanPrimary", Name = "Contact Lens - Vision Plan Primary", Description = "Contact lens fitting/materials using vision plan", SortOrder = 20 },
                new LookupItem { Code = "Glasses_UseVisionPlanPrimary", Name = "Glasses - Vision Plan Primary", Description = "Eyeglasses using vision plan as primary", SortOrder = 30 },
                
                // Medical-first scenarios
                new LookupItem { Code = "Medical_UseMedicalPlanPrimary", Name = "Medical Eye Condition - Medical Plan Primary", Description = "Medical eye condition using medical plan as primary", SortOrder = 40 },
                new LookupItem { Code = "Medical_UseMedicarePrimary", Name = "Medical Eye Condition - Medicare Primary", Description = "Medical eye condition using Medicare as primary", SortOrder = 50 },
                new LookupItem { Code = "Diabetic_UseMedicarePrimary", Name = "Diabetic Eye Exam - Medicare Primary", Description = "Diabetic retinopathy screening using Medicare", SortOrder = 60 },
                
                // Single coverage scenarios
                new LookupItem { Code = "SingleCoverage_PrimaryVision", Name = "Single Coverage - Vision Only", Description = "Patient has only vision coverage", SortOrder = 70 },
                new LookupItem { Code = "SingleCoverage_PrimaryMedical", Name = "Single Coverage - Medical Only", Description = "Patient has only medical coverage", SortOrder = 80 },
                
                // Special scenarios
                new LookupItem { Code = "UserOverride_ManualSelection", Name = "User Override - Manual Selection", Description = "Coverage decision manually overridden by user", SortOrder = 90 },
                new LookupItem { Code = "MedicareSupplemental_Secondary", Name = "Medicare + Supplemental", Description = "Medicare primary with supplemental/Medigap secondary", SortOrder = 100 },
                new LookupItem { Code = "VisionThenMedical_DualCoverage", Name = "Vision Primary, Medical Secondary", Description = "Vision plan primary with medical as secondary", SortOrder = 110 },
                new LookupItem { Code = "MedicalThenVision_DualCoverage", Name = "Medical Primary, Vision Secondary", Description = "Medical plan primary with vision as secondary", SortOrder = 120 }
            }
        }
    };

#endregion





























