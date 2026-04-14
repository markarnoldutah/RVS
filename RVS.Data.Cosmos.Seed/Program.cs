// RVS.Data.Cosmos.Seed — Creates 9 Cosmos containers with partition keys,
// unique key policies, and indexing policies, then seeds realistic test data.
// Idempotent: safe to re-run (uses UpsertItemAsync and CreateContainerIfNotExistsAsync).
//
// Usage:
//   dotnet run                          → seeds Local (emulator)
//   dotnet run -- --environment Staging → seeds Staging
//
// Configuration layering:
//   appsettings.json → appsettings.{environment}.json → user secrets → command-line args

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using RVS.Domain.Entities;
using RVS.Domain.Shared;

var switchMappings = new Dictionary<string, string>
{
    { "-e", "environment" },
    { "--env", "environment" },
};

var cmdConfig = new ConfigurationBuilder()
    .AddCommandLine(args, switchMappings)
    .Build();

var environment = cmdConfig["environment"] ?? "Local";
var validEnvironments = new[] { "Local", "Staging" };
if (!validEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"✗ Unknown environment '{environment}'. Valid values: {string.Join(", ", validEnvironments)}");
    return;
}

Console.WriteLine($"🌍 Environment: {environment}");
Console.WriteLine();

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment.ToLowerInvariant()}.json", optional: false, reloadOnChange: false);

// User secrets supply sensitive values (e.g. Cosmos key) for non-local environments.
// Local emulator creds live in appsettings.local.json and are not secret.
if (!string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase))
    configBuilder.AddUserSecrets<Program>(optional: false);

configBuilder.AddCommandLine(args, switchMappings);

var configuration = configBuilder.Build();

var endpointUri = configuration["CosmosDb:Endpoint"] ?? throw new InvalidOperationException("CosmosDb:Endpoint is not configured.");
var key = configuration["CosmosDb:Key"] ?? throw new InvalidOperationException("CosmosDb:Key is not configured.");
var databaseId = configuration["CosmosDb:DatabaseId"] ?? throw new InvalidOperationException("CosmosDb:DatabaseId is not configured.");

// Container management — set to true to delete and recreate containers (WARNING: deletes all data!)
const bool DeleteContainersBeforeCreating = true;

if (!string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"⚠️  You are about to seed the {environment.ToUpperInvariant()} Cosmos database: {databaseId}");
    Console.WriteLine($"   Endpoint: {endpointUri}");
    if (DeleteContainersBeforeCreating)
        Console.WriteLine("   DeleteContainersBeforeCreating = true — ALL DATA WILL BE DELETED!");
    Console.ResetColor();
    Console.Write("\nType 'yes' to continue: ");
    var confirmation = Console.ReadLine();
    if (!string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Aborted.");
        return;
    }
    Console.WriteLine();
}

Console.WriteLine("Starting Cosmos seed...\n");

using var client = new CosmosClient(endpointUri, key);

try
{
    var database = (await client.CreateDatabaseIfNotExistsAsync(databaseId)).Database;

    // ── 1. Container definitions ────────────────────────────────────────
    var containerDefs = BuildContainerDefinitions();

    if (DeleteContainersBeforeCreating)
    {
        Console.WriteLine("⚠️  DeleteContainersBeforeCreating = true. Deleting existing containers...");
        foreach (var def in containerDefs)
            await TryDeleteContainerAsync(database, def.Id);

        await Task.Delay(1000);
        Console.WriteLine("✓ Container deletion complete.\n");
    }

    Console.WriteLine("Creating containers with optimized indexing policies...\n");

    var containers = new Dictionary<string, Container>();
    foreach (var def in containerDefs)
    {
        var response = await database.CreateContainerIfNotExistsAsync(def);
        containers[def.Id] = response.Container;
        Console.WriteLine($"  ✓ {def.Id} (PK={def.PartitionKeyPath})");
    }

    Console.WriteLine();

    // ── 2. Build all seed data ──────────────────────────────────────────
    var tenants = BuildTenants();
    var tenantConfigs = BuildTenantConfigs();
    var dealerships = BuildDealerships();
    var locations = BuildLocations();
    var slugLookups = BuildSlugLookups();
    var customerProfiles = BuildCustomerProfiles();
    var globalAccounts = BuildGlobalCustomerAccounts();
    var serviceRequests = BuildServiceRequests();
    var assetLedgerEntries = BuildAssetLedgerEntries();
    var lookupSets = BuildLookupSets();
    var warrantyRules = BuildRvWarrantyRules();

    // ── 3. Seed each container (idempotent via upsert) ──────────────────
    await SeedItemsAsync(containers["dealerships"], dealerships, d => new PartitionKey(d.TenantId), "dealerships");
    await SeedItemsAsync(containers["locations"], locations, l => new PartitionKey(l.TenantId), "locations");
    await SeedItemsAsync(containers["service-requests"], serviceRequests, sr => new PartitionKey(sr.TenantId), "service-requests");
    await SeedItemsAsync(containers["customer-profiles"], customerProfiles, cp => new PartitionKey(cp.TenantId), "customer-profiles");
    await SeedItemsAsync(containers["global-customer-accounts"], globalAccounts, ga => new PartitionKey(ga.Email), "global-customer-accounts");
    await SeedItemsAsync(containers["asset-ledger"], assetLedgerEntries, ale => new PartitionKey(ale.AssetId), "asset-ledger");
    await SeedItemsAsync(containers["slug-lookups"], slugLookups, sl => new PartitionKey(sl.Slug), "slug-lookups");
    await SeedItemsAsync(containers["tenant-configs"], tenantConfigs, tc => new PartitionKey(tc.TenantId), "tenant-configs");
    await SeedItemsAsync(containers["lookup-sets"], lookupSets, ls => new PartitionKey(ls.Category), "lookup-sets");
    await SeedItemsAsync(containers["rv-warranty-rules"], warrantyRules, wr => new PartitionKey(wr.Manufacturer), "rv-warranty-rules");

    // Tenants go into the dealerships container (same PK /tenantId, discriminated by type)
    await SeedItemsAsync(containers["dealerships"], tenants, t => new PartitionKey(t.TenantId), "tenants (in dealerships container)");

    Console.WriteLine("\n✓ Done seeding.");
    Console.WriteLine("\n📊 Container Summary:");
    foreach (var def in containerDefs)
        Console.WriteLine($"  • {def.Id}: PK={def.PartitionKeyPath}");
}
catch (CosmosException ex)
{
    Console.WriteLine($"✗ Cosmos DB Error: {ex.StatusCode} — {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}

// ═══════════════════════════════════════════════════════════════════════════
// Helper methods
// ═══════════════════════════════════════════════════════════════════════════

static async Task TryDeleteContainerAsync(Database database, string containerId)
{
    try
    {
        await database.GetContainer(containerId).DeleteContainerAsync();
        Console.WriteLine($"  ✓ Deleted container: {containerId}");
    }
    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        Console.WriteLine($"  ○ Container not found: {containerId}");
    }
}

static async Task SeedItemsAsync<T>(Container container, List<T> items, Func<T, PartitionKey> pkSelector, string label)
{
    Console.WriteLine($"Seeding {label} ({items.Count} items)...");
    foreach (var item in items)
        await container.UpsertItemAsync(item, pkSelector(item));
}

// ═══════════════════════════════════════════════════════════════════════════
// Container definitions — 9 containers
// ═══════════════════════════════════════════════════════════════════════════

static List<ContainerProperties> BuildContainerDefinitions()
{
    return
    [
        // 1. service-requests — most queried, PK=/tenantId
        new ContainerProperties
        {
            Id = "service-requests",
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/status/?" },
                    new IncludedPath { Path = "/locationId/?" },
                    new IncludedPath { Path = "/customerProfileId/?" },
                    new IncludedPath { Path = "/issueCategory/?" },
                    new IncludedPath { Path = "/createdAtUtc/?" },
                    new IncludedPath { Path = "/scheduledDateUtc/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new() { Path = "/status", Order = CompositePathSortOrder.Ascending },
                        new() { Path = "/createdAtUtc", Order = CompositePathSortOrder.Descending },
                    },
                    new Collection<CompositePath>
                    {
                        new() { Path = "/locationId", Order = CompositePathSortOrder.Ascending },
                        new() { Path = "/status", Order = CompositePathSortOrder.Ascending },
                    },
                },
            },
        },

        // 2. customer-profiles — PK=/tenantId, unique on [/tenantId, /email]
        new ContainerProperties
        {
            Id = "customer-profiles",
            PartitionKeyPath = "/tenantId",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/tenantId", "/email" } } }
            },
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/email/?" },
                    new IncludedPath { Path = "/globalCustomerAcctId/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 3. global-customer-accounts — PK=/email (one record per real human; email is always normalized)
        new ContainerProperties
        {
            Id = "global-customer-accounts",
            PartitionKeyPath = "/email",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/email/?" },
                    new IncludedPath { Path = "/magicLinkToken/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 4. asset-ledger — PK=/assetId, unique on [/assetId, /serviceRequestId] (write-once per SR)
        new ContainerProperties
        {
            Id = "asset-ledger",
            PartitionKeyPath = "/assetId",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/assetId", "/serviceRequestId" } } }
            },
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/assetId/?" },
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/serviceRequestId/?" },
                    new IncludedPath { Path = "/submittedAtUtc/?" },
                    new IncludedPath { Path = "/status/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 5. dealerships — PK=/tenantId (also holds Tenant docs via type discriminator)
        new ContainerProperties
        {
            Id = "dealerships",
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/type/?" },
                    new IncludedPath { Path = "/slug/?" },
                    new IncludedPath { Path = "/name/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new() { Path = "/type", Order = CompositePathSortOrder.Ascending },
                        new() { Path = "/name", Order = CompositePathSortOrder.Ascending },
                    },
                },
            },
        },

        // 6. locations — PK=/tenantId, unique on [/tenantId, /slug]
        new ContainerProperties
        {
            Id = "locations",
            PartitionKeyPath = "/tenantId",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/tenantId", "/slug" } } }
            },
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/slug/?" },
                    new IncludedPath { Path = "/type/?" },
                    new IncludedPath { Path = "/name/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 7. slug-lookups — PK=/slug (point reads by slug)
        new ContainerProperties
        {
            Id = "slug-lookups",
            PartitionKeyPath = "/slug",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/slug/?" },
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/locationId/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 8. tenant-configs — PK=/tenantId (point reads by tenantId)
        new ContainerProperties
        {
            Id = "tenant-configs",
            PartitionKeyPath = "/tenantId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },

        // 9. lookup-sets — PK=/category (grouped by category for global + tenant override queries)
        new ContainerProperties
        {
            Id = "lookup-sets",
            PartitionKeyPath = "/category",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/category/?" },
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new() { Path = "/category", Order = CompositePathSortOrder.Ascending },
                        new() { Path = "/name", Order = CompositePathSortOrder.Ascending },
                    },
                },
            },
        },

        // 10. rv-warranty-rules — PK=/manufacturer (global reference data for RV warranty rules)
        new ContainerProperties
        {
            Id = "rv-warranty-rules",
            PartitionKeyPath = "/manufacturer",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/manufacturer/?" },
                    new IncludedPath { Path = "/brandDivision/?" },
                    new IncludedPath { Path = "/type/?" },
                },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/*" },
                    new ExcludedPath { Path = "/_etag/?" },
                },
            },
        },
    ];
}

// ═══════════════════════════════════════════════════════════════════════════
// Seed data builders
// ═══════════════════════════════════════════════════════════════════════════

// ── Stable IDs ──────────────────────────────────────────────────────────
// Tenants
const string TenantBlueCompass = "ten_bluecompass";
const string TenantHappyTrails = "ten_happytrails";

// Locations
const string LocSaltLake = "loc_slc";
const string LocDenver = "loc_den";
const string LocLasVegas = "loc_lv";
const string LocPhoenix = "loc_phx";
const string LocBoise = "loc_boi";

// Global customer account IDs
const string GcaJohnson = "gca_johnson";
const string GcaSmith = "gca_smith";
const string GcaMartinez = "gca_martinez";
const string GcaWilliams = "gca_williams";
const string GcaThompson = "gca_thompson";
const string GcaChen = "gca_chen";

// Customer profile IDs
const string CpJohnsonBc = "cp_johnson_bc";
const string CpSmithBc = "cp_smith_bc";
const string CpMartinezBc = "cp_martinez_bc";
const string CpWilliamsHt = "cp_williams_ht";
const string CpThompsonHt = "cp_thompson_ht";
const string CpChenBc = "cp_chen_bc";

// Sample AssetIds (VIN format — 17-character Vehicle Identification Numbers)
const string AssetId1 = "5SFCU2324GE004561";
const string AssetId2 = "5XWTF2147HF019873";
const string AssetId3 = "5ZT2FJ1B9JA003417";
const string AssetId4 = "5NHUH2620KE010592";
const string AssetId5 = "5RVMK2510LE002846";
const string AssetId6 = "5B4MP6700ME001739";
const string AssetId7 = "5LZBK2112NE006485";
const string AssetId8 = "5KTGP2426PE013260";

// Service request IDs (randomized GUIDs)
const string Sr01 = "a7c3e1f0-84b2-4d6a-9e5f-1b3c7d9a2e40";
const string Sr02 = "d4f8b6a2-3c91-47e5-b0d7-8e2f5a6c1d39";
const string Sr03 = "f1e9c5b7-62d0-4a83-8f4e-0d7b3c8a9e21";
const string Sr04 = "b8d2a4e6-c701-4f59-ab3c-6e9d1f5b7a08";
const string Sr05 = "c5a7f3d1-9e48-4b26-80dc-2f6a8e4c3b17";
const string Sr06 = "e3b1d9c5-07f6-42a4-9d8e-4a7c2f1b5e63";
const string Sr07 = "917a4e2c-b5d8-4f03-a6c1-3d0e9f7b8a24";
const string Sr08 = "f6c8a0d4-2e17-4b93-85af-9d3c1e6f7b52";
const string Sr09 = "a2e4c6f8-d013-4a75-b9d2-7f8e3b1a5c40";
const string Sr10 = "d0b7e9a3-f582-46c1-8d4f-5a2c9e6b3d18";
const string Sr11 = "c9f1b3d5-a764-4e02-87c6-0e8d4f2a1b79";
const string Sr12 = "b5d7a9e1-c083-4f46-92ab-3f6c8e1d0a57";
const string Sr13 = "e8a2c4f6-1db5-4973-b0e7-6d9a3f5c2b84";
const string Sr14 = "a4c6e8b0-f2d7-4a19-83c5-1b0e9d7f4a63";

static DateTime SeedDate(int daysAgo) => DateTime.UtcNow.AddDays(-daysAgo);

// ── Tenants ─────────────────────────────────────────────────────────────

static List<Tenant> BuildTenants() =>
[
    new Tenant
    {
        Id = TenantBlueCompass,
        Name = "Blue Compass RV",
        BillingEmail = "billing@bluecompassrv.com",
        Status = "Active",
        Plan = "Enterprise",
        CreatedByUserId = "seed",
    },
    new Tenant
    {
        Id = TenantHappyTrails,
        Name = "Happy Trails RV",
        BillingEmail = "admin@happytrailsrv.com",
        Status = "Active",
        Plan = "Professional",
        CreatedByUserId = "seed",
    },
];

// ── Tenant Configs ──────────────────────────────────────────────────────

static List<TenantConfig> BuildTenantConfigs() =>
[
    new TenantConfig
    {
        Id = $"{TenantBlueCompass}_config",
        TenantId = TenantBlueCompass,
        CreatedByUserId = "seed",
        AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true },
    },
    new TenantConfig
    {
        Id = $"{TenantHappyTrails}_config",
        TenantId = TenantHappyTrails,
        CreatedByUserId = "seed",
        AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true },
    },
];

// ── Dealerships ─────────────────────────────────────────────────────────

static List<Dealership> BuildDealerships() =>
[
    new Dealership
    {
        Id = "dlr_bluecompass",
        TenantId = TenantBlueCompass,
        Name = "Blue Compass RV",
        Slug = "blue-compass-rv",
        ServiceEmail = "service@bluecompassrv.com",
        Phone = "(801) 555-0100",
        CreatedByUserId = "seed",
        IntakeConfig = new IntakeFormConfigEmbedded
        {
            MaxFileSizeMb = 50,
            MaxAttachments = 10,
            AiContext = "Large multi-location RV dealership chain in the Western US.",
        },
    },
    new Dealership
    {
        Id = "dlr_happytrails",
        TenantId = TenantHappyTrails,
        Name = "Happy Trails RV",
        Slug = "happy-trails-rv",
        ServiceEmail = "service@happytrailsrv.com",
        Phone = "(208) 555-0200",
        CreatedByUserId = "seed",
        IntakeConfig = new IntakeFormConfigEmbedded
        {
            MaxFileSizeMb = 25,
            MaxAttachments = 5,
            AiContext = "Family-owned single-location RV service center in Boise, Idaho.",
        },
    },
];

// ── Locations (5) ───────────────────────────────────────────────────────

static List<Location> BuildLocations() =>
[
    // Blue Compass — 4 locations
    new Location
    {
        Id = LocSaltLake,
        TenantId = TenantBlueCompass,
        Name = "Blue Compass RV — Salt Lake City",
        Slug = "blue-compass-slc",
        Phone = "(801) 555-0101",
        CreatedByUserId = "seed",
        Address = new AddressEmbedded
        {
            Address1 = "4500 S State St",
            City = "Salt Lake City",
            State = "UT",
            PostalCode = "84107",
        },
    },
    new Location
    {
        Id = LocDenver,
        TenantId = TenantBlueCompass,
        Name = "Blue Compass RV — Denver",
        Slug = "blue-compass-den",
        Phone = "(303) 555-0102",
        CreatedByUserId = "seed",
        Address = new AddressEmbedded
        {
            Address1 = "7800 E Iliff Ave",
            City = "Denver",
            State = "CO",
            PostalCode = "80231",
        },
    },
    new Location
    {
        Id = LocLasVegas,
        TenantId = TenantBlueCompass,
        Name = "Blue Compass RV — Las Vegas",
        Slug = "blue-compass-lv",
        Phone = "(702) 555-0103",
        CreatedByUserId = "seed",
        Address = new AddressEmbedded
        {
            Address1 = "3200 Boulder Hwy",
            City = "Las Vegas",
            State = "NV",
            PostalCode = "89121",
        },
    },
    new Location
    {
        Id = LocPhoenix,
        TenantId = TenantBlueCompass,
        Name = "Blue Compass RV — Phoenix",
        Slug = "blue-compass-phx",
        Phone = "(602) 555-0104",
        CreatedByUserId = "seed",
        Address = new AddressEmbedded
        {
            Address1 = "1520 W Camelback Rd",
            City = "Phoenix",
            State = "AZ",
            PostalCode = "85015",
        },
    },

    // Happy Trails — 1 location
    new Location
    {
        Id = LocBoise,
        TenantId = TenantHappyTrails,
        Name = "Happy Trails RV — Boise",
        Slug = "happy-trails-boise",
        Phone = "(208) 555-0201",
        CreatedByUserId = "seed",
        Address = new AddressEmbedded
        {
            Address1 = "2901 W Elder St",
            City = "Boise",
            State = "ID",
            PostalCode = "83705",
        },
    },
];

// ── Slug Lookups (one per location) ─────────────────────────────────────

static List<SlugLookup> BuildSlugLookups() =>
[
    new SlugLookup
    {
        Id = "slug_blue-compass-slc",
        TenantId = TenantBlueCompass,
        Slug = "blue-compass-slc",
        LocationId = LocSaltLake,
        DealershipName = "Blue Compass RV",
        LocationName = "Blue Compass RV — Salt Lake City",
        CreatedByUserId = "seed",
    },
    new SlugLookup
    {
        Id = "slug_blue-compass-den",
        TenantId = TenantBlueCompass,
        Slug = "blue-compass-den",
        LocationId = LocDenver,
        DealershipName = "Blue Compass RV",
        LocationName = "Blue Compass RV — Denver",
        CreatedByUserId = "seed",
    },
    new SlugLookup
    {
        Id = "slug_blue-compass-lv",
        TenantId = TenantBlueCompass,
        Slug = "blue-compass-lv",
        LocationId = LocLasVegas,
        DealershipName = "Blue Compass RV",
        LocationName = "Blue Compass RV — Las Vegas",
        CreatedByUserId = "seed",
    },
    new SlugLookup
    {
        Id = "slug_blue-compass-phx",
        TenantId = TenantBlueCompass,
        Slug = "blue-compass-phx",
        LocationId = LocPhoenix,
        DealershipName = "Blue Compass RV",
        LocationName = "Blue Compass RV — Phoenix",
        CreatedByUserId = "seed",
    },
    new SlugLookup
    {
        Id = "slug_happy-trails-boise",
        TenantId = TenantHappyTrails,
        Slug = "happy-trails-boise",
        LocationId = LocBoise,
        DealershipName = "Happy Trails RV",
        LocationName = "Happy Trails RV — Boise",
        CreatedByUserId = "seed",
    },
];

// ── Global Customer Accounts (5) ────────────────────────────────────────

static List<GlobalCustomerAcct> BuildGlobalCustomerAccounts() =>
[
    new GlobalCustomerAcct
    {
        Id = GcaJohnson,
        Email = "mike.johnson@example.com",
        FirstName = "Mike",
        LastName = "Johnson",
        Phone = "(801) 555-1001",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_johnson_abc123def456",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId1],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantBlueCompass,
                ProfileId = CpJohnsonBc,
                DealershipName = "Blue Compass RV",
                FirstSeenAtUtc = SeedDate(90),
                RequestCount = 3,
            },
        ],
    },
    new GlobalCustomerAcct
    {
        Id = GcaSmith,
        Email = "sarah.smith@example.com",
        FirstName = "Sarah",
        LastName = "Smith",
        Phone = "(303) 555-1002",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_smith_ghi789jkl012",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId2],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantBlueCompass,
                ProfileId = CpSmithBc,
                DealershipName = "Blue Compass RV",
                FirstSeenAtUtc = SeedDate(60),
                RequestCount = 2,
            },
        ],
    },
    new GlobalCustomerAcct
    {
        Id = GcaMartinez,
        Email = "carlos.martinez@example.com",
        FirstName = "Carlos",
        LastName = "Martinez",
        Phone = "(702) 555-1003",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_martinez_mno345pqr678",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId3],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantBlueCompass,
                ProfileId = CpMartinezBc,
                DealershipName = "Blue Compass RV",
                FirstSeenAtUtc = SeedDate(30),
                RequestCount = 2,
            },
        ],
    },
    new GlobalCustomerAcct
    {
        Id = GcaWilliams,
        Email = "jenny.williams@example.com",
        FirstName = "Jenny",
        LastName = "Williams",
        Phone = "(208) 555-1004",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_williams_stu901vwx234",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId4],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantHappyTrails,
                ProfileId = CpWilliamsHt,
                DealershipName = "Happy Trails RV",
                FirstSeenAtUtc = SeedDate(45),
                RequestCount = 2,
            },
        ],
    },
    new GlobalCustomerAcct
    {
        Id = GcaThompson,
        Email = "dave.thompson@example.com",
        FirstName = "Dave",
        LastName = "Thompson",
        Phone = "(208) 555-1005",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_thompson_yza567bcd890",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId5],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantHappyTrails,
                ProfileId = CpThompsonHt,
                DealershipName = "Happy Trails RV",
                FirstSeenAtUtc = SeedDate(20),
                RequestCount = 1,
            },
        ],
    },
    new GlobalCustomerAcct
    {
        Id = GcaChen,
        Email = "lisa.chen@example.com",
        FirstName = "Lisa",
        LastName = "Chen",
        Phone = "(602) 555-1006",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_chen_efg123hij456",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownAssetIds = [AssetId6, AssetId7, AssetId8],
        LinkedProfiles =
        [
            new LinkedProfileEmbedded
            {
                TenantId = TenantBlueCompass,
                ProfileId = CpChenBc,
                DealershipName = "Blue Compass RV",
                FirstSeenAtUtc = SeedDate(120),
                RequestCount = 2,
            },
        ],
    },
];

// ── Customer Profiles (5) ───────────────────────────────────────────────

static List<CustomerProfile> BuildCustomerProfiles() =>
[
    // Blue Compass profiles (3)
    new CustomerProfile
    {
        Id = CpJohnsonBc,
        TenantId = TenantBlueCompass,
        Name = "Mike Johnson",
        Email = "mike.johnson@example.com",
        FirstName = "Mike",
        LastName = "Johnson",
        Phone = "(801) 555-1001",
        GlobalCustomerAcctId = GcaJohnson,
        CreatedByUserId = "seed",
        TotalRequestCount = 3,
        ServiceRequestIds = [Sr01, Sr02, Sr03],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId1,
                Manufacturer = "Winnebago",
                Model = "View 24D",
                Year = 2023,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(90),
                LastSeenAtUtc = SeedDate(5),
                RequestCount = 3,
            },
        ],
    },
    new CustomerProfile
    {
        Id = CpSmithBc,
        TenantId = TenantBlueCompass,
        Name = "Sarah Smith",
        Email = "sarah.smith@example.com",
        FirstName = "Sarah",
        LastName = "Smith",
        Phone = "(303) 555-1002",
        GlobalCustomerAcctId = GcaSmith,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = [Sr04, Sr05],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId2,
                Manufacturer = "Airstream",
                Model = "Interstate 24GL",
                Year = 2022,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(60),
                LastSeenAtUtc = SeedDate(10),
                RequestCount = 2,
            },
        ],
    },
    new CustomerProfile
    {
        Id = CpMartinezBc,
        TenantId = TenantBlueCompass,
        Name = "Carlos Martinez",
        Email = "carlos.martinez@example.com",
        FirstName = "Carlos",
        LastName = "Martinez",
        Phone = "(702) 555-1003",
        GlobalCustomerAcctId = GcaMartinez,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = [Sr06, Sr07],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId3,
                Manufacturer = "Thor Motor Coach",
                Model = "Chateau 22E",
                Year = 2024,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(30),
                LastSeenAtUtc = SeedDate(3),
                RequestCount = 2,
            },
        ],
    },

    // Happy Trails profiles (2)
    new CustomerProfile
    {
        Id = CpWilliamsHt,
        TenantId = TenantHappyTrails,
        Name = "Jenny Williams",
        Email = "jenny.williams@example.com",
        FirstName = "Jenny",
        LastName = "Williams",
        Phone = "(208) 555-1004",
        GlobalCustomerAcctId = GcaWilliams,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = [Sr08, Sr09],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId4,
                Manufacturer = "Jayco",
                Model = "Jay Flight 28BHS",
                Year = 2021,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(45),
                LastSeenAtUtc = SeedDate(7),
                RequestCount = 2,
            },
        ],
    },
    new CustomerProfile
    {
        Id = CpThompsonHt,
        TenantId = TenantHappyTrails,
        Name = "Dave Thompson",
        Email = "dave.thompson@example.com",
        FirstName = "Dave",
        LastName = "Thompson",
        Phone = "(208) 555-1005",
        GlobalCustomerAcctId = GcaThompson,
        CreatedByUserId = "seed",
        TotalRequestCount = 1,
        ServiceRequestIds = [Sr10],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId5,
                Manufacturer = "Forest River",
                Model = "Rockwood Ultra Lite 2608BS",
                Year = 2023,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(20),
                LastSeenAtUtc = SeedDate(20),
                RequestCount = 1,
            },
        ],
    },

    // Lisa Chen — Blue Compass (3 RVs, 2 service requests on one RV)
    new CustomerProfile
    {
        Id = CpChenBc,
        TenantId = TenantBlueCompass,
        Name = "Lisa Chen",
        Email = "lisa.chen@example.com",
        FirstName = "Lisa",
        LastName = "Chen",
        Phone = "(602) 555-1006",
        GlobalCustomerAcctId = GcaChen,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = [Sr11, Sr12],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId6,
                Manufacturer = "Coachmen",
                Model = "Catalina Legacy 323BHDSCK",
                Year = 2022,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(120),
                LastSeenAtUtc = SeedDate(2),
                RequestCount = 2,
            },
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId7,
                Manufacturer = "Entegra Coach",
                Model = "Vision 29S",
                Year = 2025,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(60),
                LastSeenAtUtc = SeedDate(60),
                RequestCount = 0,
            },
            new AssetOwnershipEmbedded
            {
                AssetId = AssetId8,
                Manufacturer = "Newmar",
                Model = "Bay Star 3014",
                Year = 2023,
                Status = AssetOwnershipStatus.Active,
                FirstSeenAtUtc = SeedDate(90),
                LastSeenAtUtc = SeedDate(90),
                RequestCount = 0,
            },
        ],
    },
];

// ── Service Requests (10) ───────────────────────────────────────────────

static List<ServiceRequest> BuildServiceRequests() =>
[
    // SR 1 — New (SLC)
    new ServiceRequest
    {
        Id = Sr01,
        TenantId = TenantBlueCompass,
        Status = "New",
        LocationId = LocSaltLake,
        CustomerProfileId = CpJohnsonBc,
        IssueDescription = "Water heater not igniting on LP gas. Pilot lights on electric but no flame on propane.",
        IssueCategory = "Plumbing/Water Systems",
        Priority = "High",
        Name = "SR-001 Johnson Water Heater",
        CreatedByUserId = "seed",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Mike", LastName = "Johnson",
            Email = "Mike.Johnson@example.com", Phone = "(801) 555-1001",
            IsReturningCustomer = true, PriorRequestCount = 2,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
    },

    // SR 2
    new ServiceRequest
    {
        Id = Sr02,
        TenantId = TenantBlueCompass,
        Status = "InProgress",
        LocationId = LocSaltLake,
        CustomerProfileId = CpJohnsonBc,
        IssueDescription = "Slide-out not fully extending. Motor runs but stops halfway. Possible debris in track.",
        IssueCategory = "Slide-Out Systems",
        Priority = "Medium",
        Name = "SR-002 Johnson Slide-Out",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_brian",
        ScheduledDateUtc = SeedDate(-2),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Mike", LastName = "Johnson",
            Email = "Mike.Johnson@example.com", Phone = "(801) 555-1001",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Slide-Out Mechanism",
            FailureMode = "Mechanical Obstruction",
        },
    },

    // SR 3 — Completed (SLC)
    new ServiceRequest
    {
        Id = Sr03,
        TenantId = TenantBlueCompass,
        Status = "Completed",
        LocationId = LocSaltLake,
        CustomerProfileId = CpJohnsonBc,
        IssueDescription = "Annual roof inspection and sealant check.",
        IssueCategory = "Roof/Exterior",
        Priority = "Low",
        Name = "SR-003 Johnson Roof Inspection",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_brian",
        ScheduledDateUtc = SeedDate(30),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Mike", LastName = "Johnson",
            Email = "Mike.Johnson@example.com", Phone = "(801) 555-1001",
            IsReturningCustomer = false, PriorRequestCount = 0,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Roof Assembly",
            FailureMode = "Wear/Age",
            RepairAction = "Sealant Reapplication",
            LaborHours = 2.5m,
            ServiceDateUtc = SeedDate(28),
        },
    },

    // SR 4 — WaitingOnParts (Denver)
    new ServiceRequest
    {
        Id = Sr04,
        TenantId = TenantBlueCompass,
        Status = "WaitingOnParts",
        LocationId = LocDenver,
        CustomerProfileId = CpSmithBc,
        IssueDescription = "Furnace blowing cold air. Thermostat reads correctly but heat does not engage.",
        IssueCategory = "HVAC",
        Priority = "High",
        Name = "SR-004 Smith Furnace",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_maria",
        ScheduledDateUtc = SeedDate(-5),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Sarah", LastName = "Smith",
            Email = "Sarah.Smith@example.com", Phone = "(303) 555-1002",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId2, Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Furnace",
            FailureMode = "Ignition Failure",
            PartsUsed = ["Ignitor Assembly P/N 12345"],
        },
    },

    // SR 5 — Cancelled (Denver)
    new ServiceRequest
    {
        Id = Sr05,
        TenantId = TenantBlueCompass,
        Status = "Cancelled",
        LocationId = LocDenver,
        CustomerProfileId = CpSmithBc,
        IssueDescription = "Rear camera image flickering intermittently.",
        IssueCategory = "Electrical",
        Priority = "Low",
        Name = "SR-005 Smith Camera",
        CreatedByUserId = "seed",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Sarah", LastName = "Smith",
            Email = "Sarah.Smith@example.com", Phone = "(303) 555-1002",
            IsReturningCustomer = false, PriorRequestCount = 0,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId2, Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022 },
    },

    // SR 6
    new ServiceRequest
    {
        Id = Sr06,
        TenantId = TenantBlueCompass,
        Status = "InProgress",
        LocationId = LocLasVegas,
        CustomerProfileId = CpMartinezBc,
        IssueDescription = "AC compressor making loud clicking noise. Reduced cooling capacity.",
        IssueCategory = "HVAC",
        Priority = "High",
        Name = "SR-006 Martinez AC",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_james",
        ScheduledDateUtc = SeedDate(-1),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Carlos", LastName = "Martinez",
            Email = "Carlos.Martinez@example.com", Phone = "(702) 555-1003",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId3, Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "AC Compressor",
            FailureMode = "Mechanical Noise",
        },
    },

    // SR 7 — New (Phoenix)
    new ServiceRequest
    {
        Id = Sr07,
        TenantId = TenantBlueCompass,
        Status = "New",
        LocationId = LocPhoenix,
        CustomerProfileId = CpMartinezBc,
        IssueDescription = "Awning fabric tearing along the seam. Approximately 12-inch rip near roller.",
        IssueCategory = "Awning/Exterior",
        Priority = "Medium",
        Name = "SR-007 Martinez Awning",
        CreatedByUserId = "seed",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Carlos", LastName = "Martinez",
            Email = "Carlos.Martinez@example.com", Phone = "(702) 555-1003",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId3, Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024 },
    },

    // SR 8
    new ServiceRequest
    {
        Id = Sr08,
        TenantId = TenantHappyTrails,
        Status = "Completed",
        LocationId = LocBoise,
        CustomerProfileId = CpWilliamsHt,
        IssueDescription = "Fresh water tank sensor reading incorrectly. Shows full when tank is half empty.",
        IssueCategory = "Plumbing/Water Systems",
        Priority = "Medium",
        Name = "SR-008 Williams Tank Sensor",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_tom",
        ScheduledDateUtc = SeedDate(14),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jenny", LastName = "Williams",
            Email = "Jenny.Williams@example.com", Phone = "(208) 555-1004",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId4, Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Tank Sensor",
            FailureMode = "Sensor Malfunction",
            RepairAction = "Sensor Replacement",
            PartsUsed = ["Tank Sensor Kit P/N TS-2021"],
            LaborHours = 1.5m,
            ServiceDateUtc = SeedDate(12),
        },
    },

    // SR 9 — WaitingOnParts (Boise — Happy Trails)
    new ServiceRequest
    {
        Id = Sr09,
        TenantId = TenantHappyTrails,
        Status = "WaitingOnParts",
        LocationId = LocBoise,
        CustomerProfileId = CpWilliamsHt,
        IssueDescription = "Entry door latch mechanism broken. Door does not close securely.",
        IssueCategory = "Doors/Locks",
        Priority = "High",
        Name = "SR-009 Williams Door Latch",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_tom",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Jenny", LastName = "Williams",
            Email = "Jenny.Williams@example.com", Phone = "(208) 555-1004",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId4, Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Door Latch Assembly",
            FailureMode = "Mechanical Breakage",
        },
    },

    // SR 10 — New (Boise — Happy Trails)
    new ServiceRequest
    {
        Id = Sr10,
        TenantId = TenantHappyTrails,
        Status = "New",
        LocationId = LocBoise,
        CustomerProfileId = CpThompsonHt,
        IssueDescription = "Generator not starting. Battery fully charged, fuel tank full, but no crank.",
        IssueCategory = "Electrical",
        Priority = "High",
        Name = "SR-010 Thompson Generator",
        CreatedByUserId = "seed",
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Dave", LastName = "Thompson",
            Email = "Dave.Thompson@example.com", Phone = "(208) 555-1005",
            IsReturningCustomer = false, PriorRequestCount = 0,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId5, Manufacturer = "Forest River", Model = "Rockwood Ultra Lite 2608BS", Year = 2023 },
    },

    // SR 11 — Completed (Phoenix — Blue Compass) — Chen, Coachmen Catalina
    new ServiceRequest
    {
        Id = Sr11,
        TenantId = TenantBlueCompass,
        Status = "Completed",
        LocationId = LocPhoenix,
        CustomerProfileId = CpChenBc,
        IssueDescription = "Inverter/converter not switching to shore power. Runs on battery only even when plugged in.",
        IssueCategory = "Electrical",
        Priority = "High",
        Name = "SR-011 Chen Inverter",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_james",
        ScheduledDateUtc = SeedDate(25),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Lisa", LastName = "Chen",
            Email = "Lisa.Chen@example.com", Phone = "(602) 555-1006",
            IsReturningCustomer = false, PriorRequestCount = 0,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId6, Manufacturer = "Coachmen", Model = "Catalina Legacy 323BHDSCK", Year = 2022 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Inverter/Converter",
            FailureMode = "Electrical Short",
            RepairAction = "Component Replacement",
            PartsUsed = ["Progressive Dynamics PD4655V Converter"],
            LaborHours = 3.0m,
            ServiceDateUtc = SeedDate(22),
        },
    },

    // SR 12 — InProgress (Phoenix — Blue Compass) — Chen, Coachmen Catalina (currently active)
    new ServiceRequest
    {
        Id = Sr12,
        TenantId = TenantBlueCompass,
        Status = "InProgress",
        LocationId = LocPhoenix,
        CustomerProfileId = CpChenBc,
        IssueDescription = "Slide-out room makes grinding noise when retracting. Extends fine but struggles on retract.",
        IssueCategory = "Slide-Out Systems",
        Priority = "Medium",
        Name = "SR-012 Chen Slide-Out",
        CreatedByUserId = "seed",
        AssignedTechnicianId = "tech_james",
        ScheduledDateUtc = SeedDate(-3),
        CustomerSnapshot = new CustomerSnapshotEmbedded
        {
            FirstName = "Lisa", LastName = "Chen",
            Email = "Lisa.Chen@example.com", Phone = "(602) 555-1006",
            IsReturningCustomer = true, PriorRequestCount = 1,
        },
        AssetInfo = new AssetInfoEmbedded { AssetId = AssetId6, Manufacturer = "Coachmen", Model = "Catalina Legacy 323BHDSCK", Year = 2022 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Slide-Out Mechanism",
            FailureMode = "Mechanical Noise",
        },
    },
];

// ── Asset Ledger Entries

static List<AssetLedgerEntry> BuildAssetLedgerEntries() =>
[
    new AssetLedgerEntry { Id = "ale_001", AssetId = AssetId1, TenantId = TenantBlueCompass, ServiceRequestId = Sr01, GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Plumbing/Water Systems", IssueDescription = "Water heater not igniting on LP gas.", Status = "New", SubmittedAtUtc = SeedDate(2) },
    new AssetLedgerEntry { Id = "ale_002", AssetId = AssetId1, TenantId = TenantBlueCompass, ServiceRequestId = Sr02, GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Slide-Out Systems", IssueDescription = "Slide-out not fully extending.", Status = "InProgress", SubmittedAtUtc = SeedDate(15) },
    new AssetLedgerEntry { Id = "ale_003", AssetId = AssetId1, TenantId = TenantBlueCompass, ServiceRequestId = Sr03, GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Roof/Exterior", IssueDescription = "Annual roof inspection and sealant check.", Status = "Completed", SubmittedAtUtc = SeedDate(35), Section10A = new Section10AEmbedded { ComponentType = "Roof Assembly", FailureMode = "Wear/Age", RepairAction = "Sealant Reapplication", LaborHours = 2.5m, ServiceDateUtc = SeedDate(28) } },
    new AssetLedgerEntry { Id = "ale_004", AssetId = AssetId2, TenantId = TenantBlueCompass, ServiceRequestId = Sr04, GlobalCustomerAcctId = GcaSmith, DealershipName = "Blue Compass RV", Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022, IssueCategory = "HVAC", IssueDescription = "Furnace blowing cold air.", Status = "WaitingOnParts", SubmittedAtUtc = SeedDate(10) },
    new AssetLedgerEntry { Id = "ale_005", AssetId = AssetId2, TenantId = TenantBlueCompass, ServiceRequestId = Sr05, GlobalCustomerAcctId = GcaSmith, DealershipName = "Blue Compass RV", Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022, IssueCategory = "Electrical", IssueDescription = "Rear camera image flickering.", Status = "Cancelled", SubmittedAtUtc = SeedDate(25) },
    new AssetLedgerEntry { Id = "ale_006", AssetId = AssetId3, TenantId = TenantBlueCompass, ServiceRequestId = Sr06, GlobalCustomerAcctId = GcaMartinez, DealershipName = "Blue Compass RV", Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024, IssueCategory = "HVAC", IssueDescription = "AC compressor making loud clicking noise.", Status = "InProgress", SubmittedAtUtc = SeedDate(5) },
    new AssetLedgerEntry { Id = "ale_007", AssetId = AssetId3, TenantId = TenantBlueCompass, ServiceRequestId = Sr07, GlobalCustomerAcctId = GcaMartinez, DealershipName = "Blue Compass RV", Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024, IssueCategory = "Awning/Exterior", IssueDescription = "Awning fabric tearing along the seam.", Status = "New", SubmittedAtUtc = SeedDate(3) },
    new AssetLedgerEntry { Id = "ale_008", AssetId = AssetId4, TenantId = TenantHappyTrails, ServiceRequestId = Sr08, GlobalCustomerAcctId = GcaWilliams, DealershipName = "Happy Trails RV", Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021, IssueCategory = "Plumbing/Water Systems", IssueDescription = "Fresh water tank sensor reading incorrectly.", Status = "Completed", SubmittedAtUtc = SeedDate(18), Section10A = new Section10AEmbedded { ComponentType = "Tank Sensor", FailureMode = "Sensor Malfunction", RepairAction = "Sensor Replacement", PartsUsed = ["Tank Sensor Kit P/N TS-2021"], LaborHours = 1.5m, ServiceDateUtc = SeedDate(12) } },
    new AssetLedgerEntry { Id = "ale_009", AssetId = AssetId4, TenantId = TenantHappyTrails, ServiceRequestId = Sr09, GlobalCustomerAcctId = GcaWilliams, DealershipName = "Happy Trails RV", Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021, IssueCategory = "Doors/Locks", IssueDescription = "Entry door latch mechanism broken.", Status = "WaitingOnParts", SubmittedAtUtc = SeedDate(7) },
    new AssetLedgerEntry { Id = "ale_010", AssetId = AssetId5, TenantId = TenantHappyTrails, ServiceRequestId = Sr10, GlobalCustomerAcctId = GcaThompson, DealershipName = "Happy Trails RV", Manufacturer = "Forest River", Model = "Rockwood Ultra Lite 2608BS", Year = 2023, IssueCategory = "Electrical", IssueDescription = "Generator not starting.", Status = "New", SubmittedAtUtc = SeedDate(1) },
    new AssetLedgerEntry { Id = "ale_011", AssetId = AssetId6, TenantId = TenantBlueCompass, ServiceRequestId = Sr11, GlobalCustomerAcctId = GcaChen, DealershipName = "Blue Compass RV", Manufacturer = "Coachmen", Model = "Catalina Legacy 323BHDSCK", Year = 2022, IssueCategory = "Electrical", IssueDescription = "Inverter/converter not switching to shore power.", Status = "Completed", SubmittedAtUtc = SeedDate(30), Section10A = new Section10AEmbedded { ComponentType = "Inverter/Converter", FailureMode = "Electrical Short", RepairAction = "Component Replacement", PartsUsed = ["Progressive Dynamics PD4655V Converter"], LaborHours = 3.0m, ServiceDateUtc = SeedDate(22) } },
    new AssetLedgerEntry { Id = "ale_012", AssetId = AssetId6, TenantId = TenantBlueCompass, ServiceRequestId = Sr12, GlobalCustomerAcctId = GcaChen, DealershipName = "Blue Compass RV", Manufacturer = "Coachmen", Model = "Catalina Legacy 323BHDSCK", Year = 2022, IssueCategory = "Slide-Out Systems", IssueDescription = "Slide-out room makes grinding noise when retracting.", Status = "InProgress", SubmittedAtUtc = SeedDate(2) },
    new AssetLedgerEntry { Id = "ale_013", AssetId = AssetId7, TenantId = TenantBlueCompass, ServiceRequestId = Sr13, GlobalCustomerAcctId = GcaChen, DealershipName = "Blue Compass RV", Manufacturer = "Entegra Coach", Model = "Vision 29S", Year = 2025, IssueCategory = "HVAC", IssueDescription = "Roof AC unit leaking condensation inside coach.", Status = "New", SubmittedAtUtc = SeedDate(55) },
    new AssetLedgerEntry { Id = "ale_014", AssetId = AssetId8, TenantId = TenantBlueCompass, ServiceRequestId = Sr14, GlobalCustomerAcctId = GcaChen, DealershipName = "Blue Compass RV", Manufacturer = "Newmar", Model = "Bay Star 3014", Year = 2023, IssueCategory = "Plumbing/Water Systems", IssueDescription = "Hot water tank pressure relief valve dripping.", Status = "Completed", SubmittedAtUtc = SeedDate(85), Section10A = new Section10AEmbedded { ComponentType = "Pressure Relief Valve", FailureMode = "Wear/Age", RepairAction = "Valve Replacement", PartsUsed = ["Suburban P/N 161135"], LaborHours = 1.0m, ServiceDateUtc = SeedDate(80) } },
];

// ── Lookup Sets (4) ─────────────────────────────────────────────────────

static List<LookupSet> BuildLookupSets() =>
[
    new LookupSet
    {
        Id = "issue-categories",
        TenantId = "GLOBAL",
        Category = "IssueCategory",
        Name = "Issue Categories",
        Description = "Standard issue categories for RV service requests",
        OverrideMode = LookupOverrideMode.GlobalOnly,
        CreatedByUserId = "seed",
        Items =
        [
            new LookupItem { Code = "HVAC", Name = "HVAC", Description = "Heating, ventilation, and air conditioning", SortOrder = 10 },
            new LookupItem { Code = "Plumbing/Water Systems", Name = "Plumbing/Water Systems", Description = "Fresh, grey, black water systems and fixtures", SortOrder = 20 },
            new LookupItem { Code = "Electrical", Name = "Electrical", Description = "12V/120V systems, wiring, batteries, solar", SortOrder = 30 },
            new LookupItem { Code = "Slide-Out Systems", Name = "Slide-Out Systems", Description = "Slide-out mechanisms, motors, seals", SortOrder = 40 },
            new LookupItem { Code = "Roof/Exterior", Name = "Roof/Exterior", Description = "Roof membrane, sealant, exterior panels", SortOrder = 50 },
            new LookupItem { Code = "Awning/Exterior", Name = "Awning/Exterior", Description = "Awning fabric, arms, motors", SortOrder = 60 },
            new LookupItem { Code = "Doors/Locks", Name = "Doors/Locks", Description = "Entry doors, compartment locks, latches", SortOrder = 70 },
            new LookupItem { Code = "Appliances", Name = "Appliances", Description = "Refrigerator, microwave, oven, washer/dryer", SortOrder = 80 },
            new LookupItem { Code = "Chassis/Drivetrain", Name = "Chassis/Drivetrain", Description = "Engine, transmission, brakes, suspension", SortOrder = 90 },
            new LookupItem { Code = "Other", Name = "Other", Description = "Issues not covered by other categories", SortOrder = 100 },
        ],
    },
    new LookupSet
    {
        Id = "component-types",
        TenantId = "GLOBAL",
        Category = "ComponentType",
        Name = "Component Types",
        Description = "Section 10A component type codes for service events",
        OverrideMode = LookupOverrideMode.GlobalOnly,
        CreatedByUserId = "seed",
        Items =
        [
            new LookupItem { Code = "AC Compressor", Name = "AC Compressor", SortOrder = 10 },
            new LookupItem { Code = "Furnace", Name = "Furnace", SortOrder = 20 },
            new LookupItem { Code = "Water Heater", Name = "Water Heater", SortOrder = 30 },
            new LookupItem { Code = "Slide-Out Mechanism", Name = "Slide-Out Mechanism", SortOrder = 40 },
            new LookupItem { Code = "Roof Assembly", Name = "Roof Assembly", SortOrder = 50 },
            new LookupItem { Code = "Awning Assembly", Name = "Awning Assembly", SortOrder = 60 },
            new LookupItem { Code = "Door Latch Assembly", Name = "Door Latch Assembly", SortOrder = 70 },
            new LookupItem { Code = "Tank Sensor", Name = "Tank Sensor", SortOrder = 80 },
            new LookupItem { Code = "Generator", Name = "Generator", SortOrder = 90 },
            new LookupItem { Code = "Inverter/Converter", Name = "Inverter/Converter", SortOrder = 100 },
        ],
    },
    new LookupSet
    {
        Id = "failure-modes",
        TenantId = "GLOBAL",
        Category = "FailureMode",
        Name = "Failure Modes",
        Description = "Section 10A failure mode codes for service events",
        OverrideMode = LookupOverrideMode.GlobalOnly,
        CreatedByUserId = "seed",
        Items =
        [
            new LookupItem { Code = "Mechanical Obstruction", Name = "Mechanical Obstruction", SortOrder = 10 },
            new LookupItem { Code = "Mechanical Breakage", Name = "Mechanical Breakage", SortOrder = 20 },
            new LookupItem { Code = "Mechanical Noise", Name = "Mechanical Noise", SortOrder = 30 },
            new LookupItem { Code = "Ignition Failure", Name = "Ignition Failure", SortOrder = 40 },
            new LookupItem { Code = "Sensor Malfunction", Name = "Sensor Malfunction", SortOrder = 50 },
            new LookupItem { Code = "Electrical Short", Name = "Electrical Short", SortOrder = 60 },
            new LookupItem { Code = "Wear/Age", Name = "Wear/Age", Description = "Normal wear and aging of components", SortOrder = 70 },
            new LookupItem { Code = "Water Damage", Name = "Water Damage", SortOrder = 80 },
            new LookupItem { Code = "Corrosion", Name = "Corrosion", SortOrder = 90 },
            new LookupItem { Code = "Unknown", Name = "Unknown", Description = "Root cause not yet determined", SortOrder = 100 },
        ],
    },
    new LookupSet
    {
        Id = "repair-actions",
        TenantId = "GLOBAL",
        Category = "RepairAction",
        Name = "Repair Actions",
        Description = "Section 10A repair action codes for service events",
        OverrideMode = LookupOverrideMode.GlobalOnly,
        CreatedByUserId = "seed",
        Items =
        [
            new LookupItem { Code = "Sealant Reapplication", Name = "Sealant Reapplication", SortOrder = 10 },
            new LookupItem { Code = "Sensor Replacement", Name = "Sensor Replacement", SortOrder = 20 },
            new LookupItem { Code = "Component Replacement", Name = "Component Replacement", SortOrder = 30 },
            new LookupItem { Code = "Electrical Repair", Name = "Electrical Repair", SortOrder = 40 },
            new LookupItem { Code = "Mechanical Adjustment", Name = "Mechanical Adjustment", SortOrder = 50 },
            new LookupItem { Code = "Cleaning/Flush", Name = "Cleaning/Flush", SortOrder = 60 },
            new LookupItem { Code = "Lubrication", Name = "Lubrication", SortOrder = 70 },
            new LookupItem { Code = "Software Update", Name = "Software Update", SortOrder = 80 },
            new LookupItem { Code = "Inspection Only", Name = "Inspection Only", Description = "No repair needed, inspection complete", SortOrder = 90 },
            new LookupItem { Code = "Warranty Claim", Name = "Warranty Claim", Description = "Repair covered under manufacturer warranty", SortOrder = 100 },
        ],
    },
];

// ── RV Warranty Rules (20) ──────────────────────────────────────────────

static List<RvWarrantyRule> BuildRvWarrantyRules() =>
[
    new RvWarrantyRule { Id = "wrr_01", TenantId = "GLOBAL", Name = "Thor Motor Coach", Manufacturer = "Thor Industries", BrandDivision = "Thor Motor Coach", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Mileage limits often apply", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_02", TenantId = "GLOBAL", Name = "Winnebago", Manufacturer = "Winnebago Industries", BrandDivision = "Winnebago", BaseWarranty = "1 year / 15k mi", StructuralWarranty = "3 years / 36k mi", RoofWarranty = "10 years", Notes = "One of the more structured programs", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_03", TenantId = "GLOBAL", Name = "Grand Design", Manufacturer = "Winnebago Industries", BrandDivision = "Grand Design", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Strong owner support reputation", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_04", TenantId = "GLOBAL", Name = "Forest River", Manufacturer = "Forest River", BrandDivision = "Forest River (various brands)", BaseWarranty = "1 year", StructuralWarranty = "1–3 years (varies)", RoofWarranty = "10–12 years", Notes = "Highly brand-dependent", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_05", TenantId = "GLOBAL", Name = "Forest River (BH)", Manufacturer = "Berkshire Hathaway", BrandDivision = "Forest River (parent)", BaseWarranty = "1 year", StructuralWarranty = "1–3 years", RoofWarranty = "10–12 years", Notes = "Same umbrella as above", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_06", TenantId = "GLOBAL", Name = "Keystone", Manufacturer = "Keystone RV", BrandDivision = "Keystone", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "One of the more standardized", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_07", TenantId = "GLOBAL", Name = "Fleetwood", Manufacturer = "REV Group", BrandDivision = "Fleetwood", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Diesel pushers may differ", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_08", TenantId = "GLOBAL", Name = "Holiday Rambler", Manufacturer = "REV Group", BrandDivision = "Holiday Rambler", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Similar to Fleetwood", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_09", TenantId = "GLOBAL", Name = "Tiffin", Manufacturer = "Tiffin Motorhomes", BrandDivision = "Tiffin", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Known for strong service support", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_10", TenantId = "GLOBAL", Name = "Jayco", Manufacturer = "Jayco (Thor)", BrandDivision = "Jayco", BaseWarranty = "2 years", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "One of the longest base warranties", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_11", TenantId = "GLOBAL", Name = "Entegra Coach", Manufacturer = "Jayco (Thor)", BrandDivision = "Entegra Coach", BaseWarranty = "2 years", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Premium segment", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_12", TenantId = "GLOBAL", Name = "Coachmen", Manufacturer = "Coachmen (Forest River)", BrandDivision = "Coachmen", BaseWarranty = "1 year", StructuralWarranty = "1–3 years", RoofWarranty = "10–12 years", Notes = "Entry to mid-tier", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_13", TenantId = "GLOBAL", Name = "Dutchmen", Manufacturer = "Dutchmen (Thor)", BrandDivision = "Dutchmen", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Similar to Keystone", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_14", TenantId = "GLOBAL", Name = "KZ RV", Manufacturer = "KZ RV (Thor)", BrandDivision = "KZ RV", BaseWarranty = "1–2 years", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Some models offer 2-year base", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_15", TenantId = "GLOBAL", Name = "Heartland", Manufacturer = "Heartland RV (Thor)", BrandDivision = "Heartland", BaseWarranty = "1 year", StructuralWarranty = "3 years", RoofWarranty = "10–12 years", Notes = "Popular mid-market", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_16", TenantId = "GLOBAL", Name = "Airstream", Manufacturer = "Airstream (Thor)", BrandDivision = "Airstream", BaseWarranty = "3 years", StructuralWarranty = "3 years", RoofWarranty = "N/A", Notes = "Aluminum shell, different structure", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_17", TenantId = "GLOBAL", Name = "Newmar", Manufacturer = "Newmar (Winnebago)", BrandDivision = "Newmar", BaseWarranty = "1 year", StructuralWarranty = "5 years", RoofWarranty = "10–12 years", Notes = "Higher-end diesel segment", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_18", TenantId = "GLOBAL", Name = "Dynamax", Manufacturer = "Dynamax (Forest River)", BrandDivision = "Dynamax", BaseWarranty = "1 year", StructuralWarranty = "3–5 years", RoofWarranty = "10–12 years", Notes = "Super C motorhomes", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_19", TenantId = "GLOBAL", Name = "Pleasure-Way", Manufacturer = "Pleasure-Way", BrandDivision = "Pleasure-Way", BaseWarranty = "3 years", StructuralWarranty = "5 years", RoofWarranty = "N/A", Notes = "Class B vans, premium build", CreatedByUserId = "seed" },
    new RvWarrantyRule { Id = "wrr_20", TenantId = "GLOBAL", Name = "Leisure Travel Vans", Manufacturer = "Leisure Travel Vans", BrandDivision = "Leisure Travel Vans", BaseWarranty = "2 years", StructuralWarranty = "5 years", RoofWarranty = "N/A", Notes = "High-end Class B/C", CreatedByUserId = "seed" },
];
