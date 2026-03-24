// RVS.Data.Cosmos.Seed — Creates 9 Cosmos containers with partition keys,
// unique key policies, and indexing policies, then seeds realistic test data.
// Idempotent: safe to re-run (uses UpsertItemAsync and CreateContainerIfNotExistsAsync).

using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using RVS.Domain.Entities;
using RVS.Domain.Shared;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: false)
    .Build();

var endpointUri = configuration["CosmosDb:Endpoint"] ?? throw new InvalidOperationException("CosmosDb:Endpoint is not configured.");
var key = configuration["CosmosDb:Key"] ?? throw new InvalidOperationException("CosmosDb:Key is not configured.");
var databaseId = configuration["CosmosDb:DatabaseId"] ?? throw new InvalidOperationException("CosmosDb:DatabaseId is not configured.");

// Container management — set to true to delete and recreate containers (WARNING: deletes all data!)
const bool DeleteContainersBeforeCreating = true;

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

    // ── 3. Seed each container (idempotent via upsert) ──────────────────
    await SeedItemsAsync(containers["dealerships"], dealerships, d => new PartitionKey(d.TenantId), "dealerships");
    await SeedItemsAsync(containers["locations"], locations, l => new PartitionKey(l.TenantId), "locations");
    await SeedItemsAsync(containers["service-requests"], serviceRequests, sr => new PartitionKey(sr.TenantId), "service-requests");
    await SeedItemsAsync(containers["customer-profiles"], customerProfiles, cp => new PartitionKey(cp.TenantId), "customer-profiles");
    await SeedItemsAsync(containers["global-customer-accounts"], globalAccounts, ga => new PartitionKey(ga.Id), "global-customer-accounts");
    await SeedItemsAsync(containers["asset-ledger"], assetLedgerEntries, ale => new PartitionKey(ale.Vin), "asset-ledger");
    await SeedItemsAsync(containers["slug-lookups"], slugLookups, sl => new PartitionKey(sl.Id), "slug-lookups");
    await SeedItemsAsync(containers["tenant-configs"], tenantConfigs, tc => new PartitionKey(tc.Id), "tenant-configs");
    await SeedItemsAsync(containers["lookup-sets"], lookupSets, ls => new PartitionKey(ls.Id), "lookup-sets");

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

        // 2. customer-profiles — PK=/tenantId, unique on [/normalizedEmail]
        new ContainerProperties
        {
            Id = "customer-profiles",
            PartitionKeyPath = "/tenantId",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/normalizedEmail" } } }
            },
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/tenantId/?" },
                    new IncludedPath { Path = "/normalizedEmail/?" },
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

        // 3. global-customer-accounts — PK=/id (point reads by id)
        new ContainerProperties
        {
            Id = "global-customer-accounts",
            PartitionKeyPath = "/id",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/normalizedEmail" } } }
            },
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/normalizedEmail/?" },
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

        // 4. asset-ledger — PK=/vin (write-once per service request)
        new ContainerProperties
        {
            Id = "asset-ledger",
            PartitionKeyPath = "/vin",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths =
                {
                    new IncludedPath { Path = "/vin/?" },
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

        // 6. locations — PK=/tenantId, unique on [/slug]
        new ContainerProperties
        {
            Id = "locations",
            PartitionKeyPath = "/tenantId",
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys = { new UniqueKey { Paths = { "/slug" } } }
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

        // 7. slug-lookups — PK=/id (point reads by slug id)
        new ContainerProperties
        {
            Id = "slug-lookups",
            PartitionKeyPath = "/id",
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

        // 8. tenant-configs — PK=/id (point reads by tenantConfig id)
        new ContainerProperties
        {
            Id = "tenant-configs",
            PartitionKeyPath = "/id",
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

        // 9. lookup-sets — PK=/id (point reads by lookupSet id)
        new ContainerProperties
        {
            Id = "lookup-sets",
            PartitionKeyPath = "/id",
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

// Customer profile IDs
const string CpJohnsonBc = "cp_johnson_bc";
const string CpSmithBc = "cp_smith_bc";
const string CpMartinezBc = "cp_martinez_bc";
const string CpWilliamsHt = "cp_williams_ht";
const string CpThompsonHt = "cp_thompson_ht";

// Sample VINs (valid 17-char format, no I/O/Q)
const string Vin1 = "1FTFW1ET5EKE12345";
const string Vin2 = "5TFCZ5AN3HX054321";
const string Vin3 = "WBA3A5C55FK198765";
const string Vin4 = "1C4RJFBG7EC234567";
const string Vin5 = "2GCEC19T441345678";

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
        NormalizedEmail = "mike.johnson@example.com",
        Email = "Mike.Johnson@example.com",
        FirstName = "Mike",
        LastName = "Johnson",
        Phone = "(801) 555-1001",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_johnson_abc123def456",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownVins = [Vin1],
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
        NormalizedEmail = "sarah.smith@example.com",
        Email = "Sarah.Smith@example.com",
        FirstName = "Sarah",
        LastName = "Smith",
        Phone = "(303) 555-1002",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_smith_ghi789jkl012",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownVins = [Vin2],
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
        NormalizedEmail = "carlos.martinez@example.com",
        Email = "Carlos.Martinez@example.com",
        FirstName = "Carlos",
        LastName = "Martinez",
        Phone = "(702) 555-1003",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_martinez_mno345pqr678",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownVins = [Vin3],
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
        NormalizedEmail = "jenny.williams@example.com",
        Email = "Jenny.Williams@example.com",
        FirstName = "Jenny",
        LastName = "Williams",
        Phone = "(208) 555-1004",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_williams_stu901vwx234",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownVins = [Vin4],
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
        NormalizedEmail = "dave.thompson@example.com",
        Email = "Dave.Thompson@example.com",
        FirstName = "Dave",
        LastName = "Thompson",
        Phone = "(208) 555-1005",
        CreatedByUserId = "seed",
        MagicLinkToken = "mlk_thompson_yza567bcd890",
        MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        AllKnownVins = [Vin5],
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
        Email = "Mike.Johnson@example.com",
        NormalizedEmail = "mike.johnson@example.com",
        FirstName = "Mike",
        LastName = "Johnson",
        Phone = "(801) 555-1001",
        GlobalCustomerAcctId = GcaJohnson,
        CreatedByUserId = "seed",
        TotalRequestCount = 3,
        ServiceRequestIds = ["sr_001", "sr_002", "sr_003"],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                Vin = Vin1,
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
        Email = "Sarah.Smith@example.com",
        NormalizedEmail = "sarah.smith@example.com",
        FirstName = "Sarah",
        LastName = "Smith",
        Phone = "(303) 555-1002",
        GlobalCustomerAcctId = GcaSmith,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = ["sr_004", "sr_005"],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                Vin = Vin2,
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
        Email = "Carlos.Martinez@example.com",
        NormalizedEmail = "carlos.martinez@example.com",
        FirstName = "Carlos",
        LastName = "Martinez",
        Phone = "(702) 555-1003",
        GlobalCustomerAcctId = GcaMartinez,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = ["sr_006", "sr_007"],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                Vin = Vin3,
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
        Email = "Jenny.Williams@example.com",
        NormalizedEmail = "jenny.williams@example.com",
        FirstName = "Jenny",
        LastName = "Williams",
        Phone = "(208) 555-1004",
        GlobalCustomerAcctId = GcaWilliams,
        CreatedByUserId = "seed",
        TotalRequestCount = 2,
        ServiceRequestIds = ["sr_008", "sr_009"],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                Vin = Vin4,
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
        Email = "Dave.Thompson@example.com",
        NormalizedEmail = "dave.thompson@example.com",
        FirstName = "Dave",
        LastName = "Thompson",
        Phone = "(208) 555-1005",
        GlobalCustomerAcctId = GcaThompson,
        CreatedByUserId = "seed",
        TotalRequestCount = 1,
        ServiceRequestIds = ["sr_010"],
        AssetsOwned =
        [
            new AssetOwnershipEmbedded
            {
                Vin = Vin5,
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
];

// ── Service Requests (10) ───────────────────────────────────────────────

static List<ServiceRequest> BuildServiceRequests() =>
[
    // SR 1 — New (SLC)
    new ServiceRequest
    {
        Id = "sr_001",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
    },

    // SR 2 — InProgress (SLC)
    new ServiceRequest
    {
        Id = "sr_002",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Slide-Out Mechanism",
            FailureMode = "Mechanical Obstruction",
        },
    },

    // SR 3 — Completed (SLC)
    new ServiceRequest
    {
        Id = "sr_003",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin1, Manufacturer = "Winnebago", Model = "View 24D", Year = 2023 },
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
        Id = "sr_004",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin2, Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022 },
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
        Id = "sr_005",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin2, Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022 },
    },

    // SR 6 — InProgress (Las Vegas)
    new ServiceRequest
    {
        Id = "sr_006",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin3, Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "AC Compressor",
            FailureMode = "Mechanical Noise",
        },
    },

    // SR 7 — New (Phoenix)
    new ServiceRequest
    {
        Id = "sr_007",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin3, Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024 },
    },

    // SR 8 — Completed (Boise — Happy Trails)
    new ServiceRequest
    {
        Id = "sr_008",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin4, Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021 },
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
        Id = "sr_009",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin4, Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021 },
        ServiceEvent = new ServiceEventEmbedded
        {
            ComponentType = "Door Latch Assembly",
            FailureMode = "Mechanical Breakage",
        },
    },

    // SR 10 — New (Boise — Happy Trails)
    new ServiceRequest
    {
        Id = "sr_010",
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
        AssetInfo = new AssetInfoEmbedded { Vin = Vin5, Manufacturer = "Forest River", Model = "Rockwood Ultra Lite 2608BS", Year = 2023 },
    },
];

// ── Asset Ledger Entries (one per SR) ───────────────────────────────────

static List<AssetLedgerEntry> BuildAssetLedgerEntries() =>
[
    new AssetLedgerEntry { Id = "ale_001", Vin = Vin1, TenantId = TenantBlueCompass, ServiceRequestId = "sr_001", GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Plumbing/Water Systems", IssueDescription = "Water heater not igniting on LP gas.", Status = "New", SubmittedAtUtc = SeedDate(2) },
    new AssetLedgerEntry { Id = "ale_002", Vin = Vin1, TenantId = TenantBlueCompass, ServiceRequestId = "sr_002", GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Slide-Out Systems", IssueDescription = "Slide-out not fully extending.", Status = "InProgress", SubmittedAtUtc = SeedDate(15) },
    new AssetLedgerEntry { Id = "ale_003", Vin = Vin1, TenantId = TenantBlueCompass, ServiceRequestId = "sr_003", GlobalCustomerAcctId = GcaJohnson, DealershipName = "Blue Compass RV", Manufacturer = "Winnebago", Model = "View 24D", Year = 2023, IssueCategory = "Roof/Exterior", IssueDescription = "Annual roof inspection and sealant check.", Status = "Completed", SubmittedAtUtc = SeedDate(35), Section10A = new Section10AEmbedded { ComponentType = "Roof Assembly", FailureMode = "Wear/Age", RepairAction = "Sealant Reapplication", LaborHours = 2.5m, ServiceDateUtc = SeedDate(28) } },
    new AssetLedgerEntry { Id = "ale_004", Vin = Vin2, TenantId = TenantBlueCompass, ServiceRequestId = "sr_004", GlobalCustomerAcctId = GcaSmith, DealershipName = "Blue Compass RV", Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022, IssueCategory = "HVAC", IssueDescription = "Furnace blowing cold air.", Status = "WaitingOnParts", SubmittedAtUtc = SeedDate(10) },
    new AssetLedgerEntry { Id = "ale_005", Vin = Vin2, TenantId = TenantBlueCompass, ServiceRequestId = "sr_005", GlobalCustomerAcctId = GcaSmith, DealershipName = "Blue Compass RV", Manufacturer = "Airstream", Model = "Interstate 24GL", Year = 2022, IssueCategory = "Electrical", IssueDescription = "Rear camera image flickering.", Status = "Cancelled", SubmittedAtUtc = SeedDate(25) },
    new AssetLedgerEntry { Id = "ale_006", Vin = Vin3, TenantId = TenantBlueCompass, ServiceRequestId = "sr_006", GlobalCustomerAcctId = GcaMartinez, DealershipName = "Blue Compass RV", Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024, IssueCategory = "HVAC", IssueDescription = "AC compressor making loud clicking noise.", Status = "InProgress", SubmittedAtUtc = SeedDate(5) },
    new AssetLedgerEntry { Id = "ale_007", Vin = Vin3, TenantId = TenantBlueCompass, ServiceRequestId = "sr_007", GlobalCustomerAcctId = GcaMartinez, DealershipName = "Blue Compass RV", Manufacturer = "Thor Motor Coach", Model = "Chateau 22E", Year = 2024, IssueCategory = "Awning/Exterior", IssueDescription = "Awning fabric tearing along the seam.", Status = "New", SubmittedAtUtc = SeedDate(3) },
    new AssetLedgerEntry { Id = "ale_008", Vin = Vin4, TenantId = TenantHappyTrails, ServiceRequestId = "sr_008", GlobalCustomerAcctId = GcaWilliams, DealershipName = "Happy Trails RV", Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021, IssueCategory = "Plumbing/Water Systems", IssueDescription = "Fresh water tank sensor reading incorrectly.", Status = "Completed", SubmittedAtUtc = SeedDate(18), Section10A = new Section10AEmbedded { ComponentType = "Tank Sensor", FailureMode = "Sensor Malfunction", RepairAction = "Sensor Replacement", PartsUsed = ["Tank Sensor Kit P/N TS-2021"], LaborHours = 1.5m, ServiceDateUtc = SeedDate(12) } },
    new AssetLedgerEntry { Id = "ale_009", Vin = Vin4, TenantId = TenantHappyTrails, ServiceRequestId = "sr_009", GlobalCustomerAcctId = GcaWilliams, DealershipName = "Happy Trails RV", Manufacturer = "Jayco", Model = "Jay Flight 28BHS", Year = 2021, IssueCategory = "Doors/Locks", IssueDescription = "Entry door latch mechanism broken.", Status = "WaitingOnParts", SubmittedAtUtc = SeedDate(7) },
    new AssetLedgerEntry { Id = "ale_010", Vin = Vin5, TenantId = TenantHappyTrails, ServiceRequestId = "sr_010", GlobalCustomerAcctId = GcaThompson, DealershipName = "Happy Trails RV", Manufacturer = "Forest River", Model = "Rockwood Ultra Lite 2608BS", Year = 2023, IssueCategory = "Electrical", IssueDescription = "Generator not starting.", Status = "New", SubmittedAtUtc = SeedDate(1) },
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
