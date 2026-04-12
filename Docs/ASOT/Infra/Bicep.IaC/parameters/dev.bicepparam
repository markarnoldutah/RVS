using '../main.bicep'

param environmentName = 'dev'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-dev-westus3'
param whisperResourceGroupName = 'rg-rvs-dev-ncus'
param openAiCapacity = 1

// Storage
param deployStorageAccount = true

// ACS
param deployAcs = true

// Static Web Apps
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-dev-westus2'
param swaSkuName = 'Free'

// DNS
param deployDns = true

// App Service (API)
param deployAppService = true
param appServicePlanSku = 'B1'
param appServiceAlwaysOn = false

// Cosmos DB
param deployCosmosDb = true
param cosmosDbSharedThroughput = 1000
param cosmosDbBackupTier = 'Continuous7Days'

// Key Vault
param deployKeyVault = true
param keyVaultPurgeProtection = false

// Observability
param deployObservability = true
param logRetentionDays = 90
param logDailyQuotaGb = 5
param enableAvailabilityTest = true
