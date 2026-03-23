// RVS.Data.Cosmos.Seed - Seeds infrastructure data (tenants, configs, lookups)

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
const string LookupsContainerId = "lookups";

// Container management - set to true to delete and recreate containers (WARNING: deletes all data!)
const bool DeleteContainersBeforeCreating = true;

// Seed control flags - set to false to skip seeding specific entity types
const bool SeedTenants = true;
const bool SeedTenantConfigs = true;
const bool SeedLookups = true;

Console.WriteLine("Starting Cosmos seed...\n");

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

    if (SeedLookups)
    {
        Console.WriteLine("Seeding lookups...");
        foreach (var l in lookupDocs)
            await lookupsContainer.Container.UpsertItemAsync(l, new PartitionKey(l.TenantId));
    }

    Console.WriteLine("\n✓ Done seeding.");
    Console.WriteLine("\n📊 Container Summary:");
    Console.WriteLine("  • Tenants: PK=/tenantId (tenant + tenantConfig documents)");
    Console.WriteLine("  • Lookups: PK=/tenantId");
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
        new Tenant { Id = "ten_001", Name = "Sample Dealership Network", BillingEmail = "billing@sampledealer.com", Status = "Active", Plan = "Enterprise", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_002", Name = "Acme Auto Group", BillingEmail = "admin@acmeauto.com", Status = "Active", Plan = "Professional", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_003", Name = "Smith Family Motors", BillingEmail = "smith@familymotors.net", Status = "Active", Plan = "Starter", CreatedByUserId = "seed-process" },
    };

static List<TenantConfig> BuildTenantConfigs() =>
    new()
    {
        new TenantConfig
        {
            Id = "ten_001_config",
            TenantId = "ten_001",
            CreatedByUserId = "seed-process",
            AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true }
        },
        new TenantConfig
        {
            Id = "ten_002_config",
            TenantId = "ten_002",
            CreatedByUserId = "seed-process",
            AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true }
        },
        new TenantConfig
        {
            Id = "ten_003_config",
            TenantId = "ten_003",
            CreatedByUserId = "seed-process",
            AccessGate = new TenantAccessGateEmbedded { LoginsEnabled = true }
        }
    };

#endregion

#region Builders - Lookups

static List<LookupSet> BuildLookups() =>
    new()
    {
        new LookupSet
        {
            Id = "service-request-statuses",
            TenantId = "GLOBAL",
            Category = "ServiceRequestStatus",
            Name = "Service Request Statuses",
            Description = "Standard status values for service requests",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                new LookupItem { Code = "New", Name = "New", Description = "Newly created service request", SortOrder = 10 },
                new LookupItem { Code = "InProgress", Name = "In Progress", Description = "Service request is being worked on", SortOrder = 20 },
                new LookupItem { Code = "OnHold", Name = "On Hold", Description = "Service request is temporarily paused", SortOrder = 30 },
                new LookupItem { Code = "Completed", Name = "Completed", Description = "Service request has been completed", SortOrder = 40 },
                new LookupItem { Code = "Cancelled", Name = "Cancelled", Description = "Service request has been cancelled", SortOrder = 50 },
            }
        }
    };

#endregion

