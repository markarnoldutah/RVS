using '../main.bicep'

param environmentName = 'dev'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-dev-westus3'
param whisperResourceGroupName = 'rg-rvs-dev-ncus'
param openAiCapacity = 1
param deployStorageAccount = true
param deployAcs = true
