using '../main.bicep'

param environmentName = 'dev'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-dev-westus3'
param whisperResourceGroupName = 'rg-rvs-dev-ncus'
param openAiCapacity = 1

// Dev uses Cosmos DB Emulator + Azurite locally; cloud resources optional
param deployAppService = false
param deployCosmosDb = false
param deployKeyVault = false
param deployObservability = false

param deployStorageAccount = true
param deployAcs = true
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-dev-westus2'
param swaSkuName = 'Free'
param deployDns = true
