// ──────────────────────────────────────────────────────────────
// Module: Static Web App custom-domain binding
// ──────────────────────────────────────────────────────────────
// Binds one custom hostname to an existing Static Web App.
//
// Split out of static-web-app.bicep on purpose. The PUT on
// Microsoft.Web/staticSites/customDomains is a long-running
// operation that waits for DNS validation to succeed — so the
// CNAME (or TXT) record it validates against must already exist
// when the PUT is issued. Keeping the binding in its own module
// lets main.bicep order it AFTER the dns.bicep module that writes
// the record, instead of before it (which deadlocks on a first
// bring-up: the binding waits for a record that a later module
// would have written).
//
// Scope: only `cname-delegation` bindings are declared from
// Bicep. The prod Intake apex (`rvintake.com`, dns-txt-token) is
// deliberately NOT declared here — see the "DNS: Intake zone"
// section of main.bicep for why, and README.md "Deploy
// Production" for the one-time apex handshake.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Static Web App in this resource group.')
param staticSiteName string

@description('Custom hostname to bind (e.g. manager-staging.rvserviceflow.com).')
param hostname string

@description('Validation method. cname-delegation for subdomains (the CNAME must already resolve); dns-txt-token for apex domains.')
@allowed([
  'cname-delegation'
  'dns-txt-token'
])
param validationMethod string = 'cname-delegation'

// ── Resources ─────────────────────────────────────────────────

resource staticSite 'Microsoft.Web/staticSites@2024-11-01' existing = {
  name: staticSiteName
}

resource binding 'Microsoft.Web/staticSites/customDomains@2024-11-01' = {
  parent: staticSite
  name: hostname
  properties: {
    validationMethod: validationMethod
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('The bound hostname.')
output hostname string = binding.name
