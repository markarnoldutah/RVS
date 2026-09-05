// ──────────────────────────────────────────────────────────────
// Module: Azure DNS Zone + configurable record set
// ──────────────────────────────────────────────────────────────
// Creates (or updates) one public DNS zone and any combination of:
//   - CNAME records (subdomains only — CNAME at apex is invalid per RFC 1034)
//   - A records     (subdomain or apex; use name="@" for apex)
//   - TXT records   (commonly used for SWA custom-domain ownership validation)
//
// The zone resource is idempotent: re-deploying with the same zoneName
// against the same resource group is a no-op on the zone itself and
// upserts each record set.
//
// Intended to be called once per zone from main.bicep — currently:
//   • rvserviceflow.com  → Manager SWA (CNAME subdomain)
//   • rvintake.com       → Intake SWA (apex A in prod, CNAME subdomain in staging)
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Public DNS zone name (e.g. rvserviceflow.com, rvintake.com).')
param zoneName string

@description('CNAME records. Each entry: { name: string, target: string }. name must NOT be "@" — CNAME at apex is invalid per RFC 1034.')
param cnameRecords array = []

@description('A records. Each entry: { name: string, ipv4Addresses: string[] }. Use name="@" for apex records (required for SWA apex since Azure DNS alias does not target Static Web Apps).')
param aRecords array = []

@description('TXT records. Each entry: { name: string, values: string[] } where values is the chunked string list composing one TXT record (per-chunk max 255 chars).')
param txtRecords array = []

@description('DNS record TTL in seconds.')
param ttl int = 3600

// ── Resources ─────────────────────────────────────────────────

resource zone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: zoneName
  location: 'global'
}

resource cnameSet 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = [for record in cnameRecords: {
  parent: zone
  name: record.name
  properties: {
    TTL: ttl
    CNAMERecord: {
      cname: record.target
    }
  }
}]

resource aSet 'Microsoft.Network/dnsZones/A@2018-05-01' = [for record in aRecords: {
  parent: zone
  name: record.name
  properties: {
    TTL: ttl
    ARecords: [for ip in record.ipv4Addresses: {
      ipv4Address: ip
    }]
  }
}]

resource txtSet 'Microsoft.Network/dnsZones/TXT@2018-05-01' = [for record in txtRecords: {
  parent: zone
  name: record.name
  properties: {
    TTL: ttl
    TXTRecords: [
      {
        value: record.values
      }
    ]
  }
}]

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the DNS zone.')
output zoneId string = zone.id

@description('Azure-assigned nameservers. Point your registrar NS records at these four values.')
output nameServers array = zone.properties.nameServers
