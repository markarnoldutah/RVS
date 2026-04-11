// ──────────────────────────────────────────────────────────────
// Module: Azure DNS Zone + SWA CNAME Records
// ──────────────────────────────────────────────────────────────
// Creates (or updates) the public DNS zone for the apex domain
// and adds CNAME records pointing each SWA subdomain at the
// Azure-assigned default hostname.
//
// Scoped to the prod primary resource group (rg-rvs-prod-westus3)
// even when called from a dev/staging deployment, because the
// apex zone is shared — only the subdomain prefix changes per env.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Public DNS zone apex domain (e.g. rvserviceflow.com).')
param zoneName string

@description('Subdomain prefix for the Intake SWA CNAME record (e.g. "intake" -> intake.rvserviceflow.com).')
param intakePrefix string

@description('Subdomain prefix for the Manager SWA CNAME record (e.g. "manager" -> manager.rvserviceflow.com).')
param managerPrefix string

@description('defaultHostname of the Intake SWA — the CNAME target (e.g. proud-rock-abc.azurestaticapps.net).')
param intakeSwaHostname string

@description('defaultHostname of the Manager SWA — the CNAME target.')
param managerSwaHostname string

@description('DNS record TTL in seconds.')
param ttl int = 3600

// ── Resources ─────────────────────────────────────────────────

resource zone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: zoneName
  location: 'global'
}

resource intakeCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: zone
  name: intakePrefix
  properties: {
    TTL: ttl
    CNAMERecord: {
      cname: intakeSwaHostname
    }
  }
}

resource managerCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: zone
  name: managerPrefix
  properties: {
    TTL: ttl
    CNAMERecord: {
      cname: managerSwaHostname
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the DNS zone.')
output zoneId string = zone.id

@description('Azure-assigned nameservers. Point your registrar NS records at these four values.')
output nameServers array = zone.properties.nameServers

@description('FQDN for the Intake SWA custom domain.')
output intakeFqdn string = '${intakePrefix}.${zoneName}'

@description('FQDN for the Manager SWA custom domain.')
output managerFqdn string = '${managerPrefix}.${zoneName}'
