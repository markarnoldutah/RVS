using '../main.bicep'

param environmentName = 'prod'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-prod-westus3'
param whisperResourceGroupName = 'rg-rvs-prod-ncus'
param openAiCapacity = 30
param deployAcs = true
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-prod-westus2'
param swaSkuName = 'Standard'
param deployDns = true
