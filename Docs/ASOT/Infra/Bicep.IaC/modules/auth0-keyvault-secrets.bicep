// ──────────────────────────────────────────────────────────────
// Module: Store Auth0 secrets in Key Vault
// ──────────────────────────────────────────────────────────────
// Auth0 is an external identity provider — there is no Azure
// resource to reference. All values are passed as secure
// parameters (typically from a parameter file or pipeline
// variables) and stored in Key Vault for the API config provider.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('The name of the existing Key Vault where secrets will be stored.')
param keyVaultName string

@secure()
@description('Auth0 tenant domain URL (e.g. https://rvs-dev.us.auth0.com).')
param auth0Domain string

@secure()
@description('Auth0 API audience identifier (e.g. https://api.rvserviceflow.com).')
param auth0Audience string

@secure()
@description('Auth0 application client ID.')
param auth0ClientId string

@secure()
@description('Auth0 application client secret.')
param auth0ClientSecret string

@secure()
@description('Auth0 token endpoint URL (e.g. https://rvs-dev.us.auth0.com/oauth/token).')
param auth0TokenUrl string

@secure()
@description('Auth0 authorization endpoint URL (e.g. https://rvs-dev.us.auth0.com/authorize).')
param auth0AuthorizationUrl string

// ── Existing Resource References ──────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

// ── Key Vault Secrets ─────────────────────────────────────────

resource auth0DomainSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--Domain'
  properties: {
    // Strip any trailing slash — endsWith guard ensures length >= 1, so BCP329 is a false positive
    #disable-next-line BCP329
    value: endsWith(auth0Domain, '/') ? substring(auth0Domain, 0, length(auth0Domain) - 1) : auth0Domain
    contentType: 'text/plain'
  }
}

resource auth0AudienceSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--Audience'
  properties: {
    value: auth0Audience
    contentType: 'text/plain'
  }
}

resource auth0ClientIdSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--ClientId'
  properties: {
    value: auth0ClientId
    contentType: 'text/plain'
  }
}

resource auth0ClientSecretKv 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--ClientSecret'
  properties: {
    value: auth0ClientSecret
    contentType: 'text/plain'
  }
}

resource auth0TokenUrlSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--TokenUrl'
  properties: {
    value: auth0TokenUrl
    contentType: 'text/plain'
  }
}

resource auth0AuthorizationUrlSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Auth0--AuthorizationUrl'
  properties: {
    value: auth0AuthorizationUrl
    contentType: 'text/plain'
  }
}
