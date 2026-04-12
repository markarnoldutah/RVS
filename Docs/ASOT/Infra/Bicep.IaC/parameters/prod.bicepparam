using '../main.bicep'

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30

// Storage
param deployStorageAccount = true

// ACS
param deployAcs = true

// Static Web Apps
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'

// DNS
param deployDns = true

// App Service (API)
param deployAppService = true
param appServicePlanSku = 'P2v3'
param appServiceAlwaysOn = true

// Cosmos DB
param deployCosmosDb = true
param cosmosDbSharedThroughput = 10000
param cosmosDbBackupTier = 'Continuous30Days'

// Key Vault
param deployKeyVault = true
param keyVaultPurgeProtection = true

// Observability
param deployObservability = true
param logRetentionDays = 90
param logDailyQuotaGb = 20
param enableAvailabilityTest = true
