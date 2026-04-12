using '../main.bicep'

param environmentName = 'staging'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-staging-westus3'
param whisperResourceGroupName = 'rg-rvs-staging-ncus'
param openAiCapacity = 10

// Storage
param deployStorageAccount = true

// ACS
param deployAcs = true

// Static Web Apps
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Standard'

// DNS
param deployDns = true

// App Service (API)
param deployAppService = true
param appServicePlanSku = 'P1v3'
param appServiceAlwaysOn = true

// Cosmos DB
param deployCosmosDb = true
param cosmosDbSharedThroughput = 4000
param cosmosDbBackupTier = 'Continuous7Days'

// Key Vault
param deployKeyVault = true
param keyVaultPurgeProtection = true

// Observability
param deployObservability = true
param logRetentionDays = 90
param logDailyQuotaGb = 10
param enableAvailabilityTest = true
