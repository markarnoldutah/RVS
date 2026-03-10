# Postman Collection - Payers Schema Migration Tests

## Overview
This Postman collection provides comprehensive API testing for the Payers container schema migration. It validates:
- GLOBAL payers accessible to all tenants (MVP)
- PayerConfigs stored per-tenant in the payers container
- Tenant isolation and data partitioning
- Schema validation and error handling

## Quick Start

### 1. Import Collection
1. Open Postman
2. Click **Import**
3. Select `Postman_Payers_Schema_Migration_Tests.json`
4. Collection will appear in your workspace

### 2. Configure Variables

The collection uses variables for easy configuration. Update these in **Collection Variables**:

| Variable | Default Value | Description |
|----------|---------------|-------------|
| `base_url` | `https://localhost:7001` | API base URL |
| `auth_token` | _(empty)_ | Bearer token for authentication |
| `tenant_001` | `ten_001` | First test tenant |
| `tenant_002` | `ten_002` | Second test tenant |
| `tenant_003` | `ten_003` | Third test tenant |
| `payer_vsp` | `payer_vsp_001` | VSP payer ID |
| `payer_bcbs` | `payer_bcbs_001` | BCBS payer ID |
| `payer_eyemed` | `payer_eyemed_001` | EyeMed payer ID |
| `payer_medicare` | `payer_medicare_001` | Medicare payer ID |

**To Update Variables:**
1. Right-click the collection ? **Edit**
2. Go to **Variables** tab
3. Update the **Current Value** column
4. Click **Save**

### 3. Set Up Authentication

If your API requires authentication:

1. **Get Auth Token:**
   - Method depends on your Auth0 configuration
   - Use Auth0 management API or login endpoint

2. **Set Token in Collection:**
   - Edit collection variables
   - Set `auth_token` to your bearer token
   - The collection uses Bearer token auth automatically

Alternatively, update individual request auth settings.

### 4. Run Tests

#### Option A: Run Entire Collection
1. Click on the collection name
2. Click **Run** button
3. Select all folders or specific ones
4. Click **Run [Collection Name]**
5. View results in the runner

#### Option B: Run Individual Folders
- **1. Payers - GLOBAL**: Tests GLOBAL payer queries
- **2. PayerConfigs - Per Tenant**: Tests tenant-specific configs
- **3. Cross-Tenant Isolation**: Validates data isolation
- **4. Schema Validation**: Validates entity schemas
- **5. Edge Cases & Error Handling**: Tests error scenarios
- **6. Integration Tests**: Tests payer + config relationships

#### Option C: Run Individual Requests
Navigate to any request and click **Send**

## Test Organization

### Folder 1: Payers - GLOBAL

Tests that GLOBAL payers work correctly across all tenants.

**Requests:**
- `GET /api/payers?tenantId={tenantId}` - Get all payers
- `GET /api/payers/{payerId}?tenantId={tenantId}` - Get specific payer
- `GET /api/payers?tenantId={tenantId}&planType=Vision` - Filter by plan type
- `GET /api/payers?tenantId={tenantId}&search=blue` - Text search

**Key Tests:**
- ? All payers have `tenantId = "GLOBAL"`
- ? Same payers returned for all tenants
- ? Contains expected payers (VSP, BCBS, EyeMed, Medicare)
- ? Filtering by plan type works
- ? Text search works

### Folder 2: PayerConfigs - Per Tenant

Tests that PayerConfigs are tenant-specific and stored in payers container.

**Requests:**
- `GET /api/config/{tenantId}/payers` - Get all configs for tenant
- `GET /api/config/{tenantId}/payers/{payerId}` - Get specific config

**Key Tests:**
- ? Each tenant has different configs
- ? Configs belong to correct tenant
- ? Configs are sorted by `sortOrder`
- ? All required fields present
- ? `ten_001` has 3 configs (VSP, EyeMed, BCBS)
- ? `ten_002` has 2 configs (VSP, BCBS)
- ? `ten_003` has 2 configs (Medicare, BCBS)

### Folder 3: Cross-Tenant Isolation

Validates that:
- GLOBAL payers are shared across tenants
- PayerConfigs are isolated per tenant

**Requests:**
- Get same payer from different tenants
- Get same payer's config from different tenants
- Verify configs don't leak across tenants

**Key Tests:**
- ? VSP payer is identical for all tenants
- ? VSP configs are different per tenant
- ? Partition key isolation works

### Folder 4: Schema Validation

Validates entity structure and data types.

**Requests:**
- Verify Payer schema
- Verify PayerConfig schema

**Key Tests:**
- ? All required fields present
- ? `type` discriminator correct
- ? `tenantId` is "GLOBAL" for payers (MVP)
- ? Audit fields present
- ? Enum values valid

### Folder 5: Edge Cases & Error Handling

Tests error scenarios and boundary conditions.

**Requests:**
- Invalid tenant ID
- Invalid payer ID
- Missing required parameters
- PayerConfig not found

**Key Tests:**
- ? Returns 400 for missing parameters
- ? Returns 404 for not found
- ? Handles invalid IDs gracefully

### Folder 6: Integration Tests

Tests relationships between Payers and PayerConfigs.

**Requests:**
- Get Payer + Config together
- Verify all configs reference valid payers

**Key Tests:**
- ? Config `payerId` matches payer `id`
- ? Config display name relates to payer name
- ? Referential integrity maintained

## Expected Test Results

After running the full collection, you should see:

| Folder | Requests | Expected Pass Rate |
|--------|----------|-------------------|
| 1. Payers - GLOBAL | 5 | 100% |
| 2. PayerConfigs - Per Tenant | 4 | 100% |
| 3. Cross-Tenant Isolation | 3 | 100% |
| 4. Schema Validation | 2 | 100% |
| 5. Edge Cases | 4 | 100% |
| 6. Integration Tests | 2 | 100% |
| **Total** | **20** | **100%** |

## Troubleshooting

### Authentication Errors (401)

**Problem:** `Status code is 401 Unauthorized`

**Solution:**
1. Verify `auth_token` variable is set
2. Ensure token hasn't expired
3. Check Auth0 configuration
4. For local development, you might need to disable auth temporarily

### Connection Errors

**Problem:** `Error: connect ECONNREFUSED`

**Solution:**
1. Verify API is running
2. Check `base_url` variable matches your API
3. For HTTPS, ensure SSL certificate is trusted (or disable SSL verification in Postman settings)

### Seed Data Not Found (404)

**Problem:** Tests fail with 404 errors

**Solution:**
1. Run the seed script first:
   ```bash
   cd BF.Data.Cosmos.Seed
   dotnet run
   ```
2. Ensure `DeleteContainersBeforeCreating = true` to start fresh
3. Verify all seed flags are `true`
4. Check Cosmos DB connection string

### Wrong Container Structure

**Problem:** Tests fail because PayerConfigs are in wrong container

**Solution:**
1. This means migration wasn't applied
2. Set `DeleteContainersBeforeCreating = true` in seed script
3. Run seed script to recreate containers with new schema
4. Verify payers container has `PartitionKeyPath = "/tenantId"`

### Tenant Isolation Failures

**Problem:** Configs from other tenants appear in results

**Solution:**
1. Verify partition key queries in repository code
2. Check that `QueryRequestOptions` uses correct partition key
3. Ensure PayerConfig entities have correct `tenantId` values

## Advanced Usage

### Running in CI/CD

Use Newman (Postman CLI) to run tests in CI/CD:

```bash
# Install Newman
npm install -g newman

# Run collection
newman run Postman_Payers_Schema_Migration_Tests.json \
  --env-var "base_url=https://your-api.azurewebsites.net" \
  --env-var "auth_token=your_token_here"

# Generate HTML report
newman run Postman_Payers_Schema_Migration_Tests.json \
  --reporters cli,html \
  --reporter-html-export report.html
```

### Using Environments

Create different Postman environments for:
- **Local**: `https://localhost:7001`
- **Dev**: `https://bf-api-dev.azurewebsites.net`
- **Staging**: `https://bf-api-staging.azurewebsites.net`

Switch environments in Postman UI to test different deployments.

### Custom Test Scripts

Requests include pre-request and test scripts that:
- Store intermediate values
- Chain requests together
- Validate complex relationships
- Generate dynamic test data

You can modify these scripts to customize testing.

## Migration Checklist

Use this checklist to validate the migration:

- [ ] Run seed script with `DeleteContainersBeforeCreating = true`
- [ ] Verify seed completes successfully
- [ ] Import Postman collection
- [ ] Configure collection variables
- [ ] Set authentication token (if needed)
- [ ] Run **Folder 1: Payers - GLOBAL** ? All tests pass
- [ ] Run **Folder 2: PayerConfigs - Per Tenant** ? All tests pass
- [ ] Run **Folder 3: Cross-Tenant Isolation** ? All tests pass
- [ ] Run **Folder 4: Schema Validation** ? All tests pass
- [ ] Run **Folder 5: Edge Cases** ? All tests pass
- [ ] Run **Folder 6: Integration Tests** ? All tests pass
- [ ] Run entire collection ? 100% pass rate
- [ ] Check Cosmos DB Data Explorer:
  - [ ] Payers container has `/tenantId` partition key
  - [ ] GLOBAL partition contains all payers
  - [ ] Each tenant partition contains PayerConfigs
  - [ ] No PayerConfigs in tenants container

## What This Tests

### ? Schema Migration Validation
- Payers container uses `/tenantId` partition key
- PayerConfig documents stored in payers container
- No PayerConfigs remain in tenants container

### ? MVP Scope Validation
- All payers have `tenantId = "GLOBAL"`
- No tenant-specific payers exist
- Extensibility for future tenant-specific payers maintained

### ? Repository Layer Validation
- `CosmosPayerRepository` queries GLOBAL partition correctly
- `CosmosConfigRepository` queries payers container for configs
- Partition key usage is correct

### ? API Layer Validation
- Endpoints return correct data
- Error handling works properly
- Query parameters work as expected

### ? Data Integrity Validation
- Payer and PayerConfig relationships maintained
- Tenant isolation enforced
- No cross-tenant data leakage

## Related Documentation

- **Schema Migration Guide**: `SCHEMA_MIGRATION_PAYERS_CONTAINER.md`
- **Seed Script Usage**: `SEED_SCRIPT_USAGE.md`
- **API Documentation**: Check Swagger at `https://localhost:7001/swagger`

## Support

If tests fail unexpectedly:
1. Check the **Test Results** tab for detailed error messages
2. Review the **Console** for request/response details
3. Verify seed data matches expected structure
4. Check repository code for correct partition key usage
5. Review API logs for server-side errors

---

**Last Updated:** 2025-01-20  
**Collection Version:** 1.0  
**Tested Against:** BF.API v1.0
