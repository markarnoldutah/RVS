// RVS.Data.Cosmos.Seed — Seeds infrastructure data into Cosmos DB
// Partition Key: /tenantId for tenants and lookups containers

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

// Container management - set to true to delete and recreate containers
const bool DeleteContainersBeforeCreating = true;

// Seed control flags
const bool SeedTenants = true;
const bool SeedTenantConfigs = true;
const bool SeedLookups = true;

Console.WriteLine("Starting RVS Cosmos seed...");

using var client = new CosmosClient(EndpointUri, Key);

try
{
    var database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);

    if (DeleteContainersBeforeCreating)
    {
        Console.WriteLine("⚠️  DeleteContainersBeforeCreating = true. Deleting existing containers...");
        await TryDeleteContainerAsync(database.Database, TenantsContainerId);
        await TryDeleteContainerAsync(database.Database, LookupsContainerId);
    }

    // Tenants: partitioned by tenantId
    var tenantsContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = TenantsContainerId,
            PartitionKeyPath = "/tenantId"
        });
    Console.WriteLine("✓ Created tenants container");

    // Lookups: partitioned by tenantId
    var lookupsContainer = await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties
        {
            Id = LookupsContainerId,
            PartitionKeyPath = "/tenantId"
        });
    Console.WriteLine("✓ Created lookups container");

    // Build seed data
    var tenants = BuildTenants();
    var tenantConfigs = BuildTenantConfigs();
    var lookupDocs = BuildLookups();

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
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    throw;
}

// =====================================================
// Helper Methods
// =====================================================

static async Task TryDeleteContainerAsync(Database database, string containerId)
{
    try
    {
        await database.GetContainer(containerId).DeleteContainerAsync();
        Console.WriteLine($"  🗑️  Deleted container: {containerId}");
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine($"  ℹ️  Container not found (skip): {containerId}");
    }
}

// =====================================================
// Seed Data Builders
// =====================================================

static List<Tenant> BuildTenants() =>
    new()
    {
        new Tenant { Id = "ten_001", Name = "Sample Dealership Network", BillingEmail = "billing@sample-dealer.com", Status = "Active", Plan = "Enterprise", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_002", Name = "Acme RV Group", BillingEmail = "admin@acmerv.com", Status = "Active", Plan = "Professional", CreatedByUserId = "seed-process" },
        new Tenant { Id = "ten_003", Name = "Smith Auto Service", BillingEmail = "smith@autoservice.net", Status = "Active", Plan = "Starter", CreatedByUserId = "seed-process" },
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

static List<LookupSet> BuildLookups() =>
    new()
    {
        new LookupSet
        {
            Id = "issue-categories",
            TenantId = "GLOBAL",
            Category = "IssueCategory",
            Name = "Issue Categories",
            Description = "Standard service request issue categories",
            OverrideMode = LookupOverrideMode.GlobalOnly,
            CreatedByUserId = "seed-process",
            Items = new List<LookupItem>
            {
                new LookupItem { Code = "ENGINE", Name = "Engine", Description = "Engine-related issues", SortOrder = 10 },
                new LookupItem { Code = "ELECTRICAL", Name = "Electrical", Description = "Electrical system issues", SortOrder = 20 },
                new LookupItem { Code = "PLUMBING", Name = "Plumbing", Description = "Plumbing issues", SortOrder = 30 },
                new LookupItem { Code = "HVAC", Name = "HVAC", Description = "Heating, ventilation, and air conditioning", SortOrder = 40 },
                new LookupItem { Code = "BODY", Name = "Body/Exterior", Description = "Body and exterior damage", SortOrder = 50 },
                new LookupItem { Code = "INTERIOR", Name = "Interior", Description = "Interior issues", SortOrder = 60 },
                new LookupItem { Code = "OTHER", Name = "Other", Description = "Other issues", SortOrder = 70 },
            }
        }
    };
