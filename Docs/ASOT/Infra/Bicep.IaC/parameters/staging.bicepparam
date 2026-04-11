using '../main.bicep'

param environmentName = 'staging'
param location = 'westus3'
param whisperLocation = 'northcentralus'
param primaryResourceGroupName = 'rg-rvs-staging-westus3'
param whisperResourceGroupName = 'rg-rvs-staging-ncus'
param openAiCapacity = 10
param deployAcs = true
param deploySwa = true
param swaLocation = 'westus2'
param swaResourceGroupName = 'rg-rvs-staging-westus2'
param swaSkuName = 'Standard'
param deployDns = true
