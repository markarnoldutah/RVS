// ──────────────────────────────────────────────────────────────
// Module: Azure DNS Zone + configurable record set
// ──────────────────────────────────────────────────────────────
// Creates (or updates) one public DNS zone and any combination of:
//   - CNAME records (subdomains only — CNAME at apex is invalid per RFC 1034)
//   - A records     (subdomain or apex; use name="@" for apex). Either a plain
//                   record with literal IPs, or an ALIAS record that targets an
//                   Azure resource by id — Azure DNS then tracks that resource's
//                   address itself, so nothing is pinned in source.
//   - TXT records   (commonly used for SWA custom-domain ownership validation)
//
// The zone resource is idempotent: re-deploying with the same zoneName
// against the same resource group is a no-op on the zone itself and
// upserts each record set.
//
// Intended to be called once per zone from main.bicep — currently:
//   • rvserviceflow.com  → Manager SWA (CNAME subdomain)
//   • rvintake.com       → Intake SWA (apex ALIAS in prod, CNAME subdomain in staging)
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Public DNS zone name (e.g. rvserviceflow.com, rvintake.com).')
param zoneName string

@description('CNAME records. Each entry: { name: string, target: string }. name must NOT be "@" — CNAME at apex is invalid per RFC 1034.')
param cnameRecords array = []

@description('A records. Each entry is EITHER { name: string, ipv4Addresses: string[] } for a literal record, OR { name: string, targetResourceId: string } for an ALIAS record that tracks an Azure resource (e.g. a Static Web App id for an apex domain). Use name="@" for the apex.')
param aRecords array = []

@description('TXT record-sets. Each entry: { name: string, values: string[] } where each value in the list is published as a SEPARATE TXT record at that name (e.g. a domain-ownership string and an SPF string sharing one host). A single string over 255 chars must be pre-split by the caller — rare.')
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
    // Exactly one of the two shapes is populated; the other serialises to
    // nothing (ARM drops null properties).
    ARecords: contains(record, 'ipv4Addresses') ? map(record.ipv4Addresses, ip => { ipv4Address: ip }) : null
    targetResource: contains(record, 'targetResourceId') ? { id: record.targetResourceId } : null
  }
}]

resource txtSet 'Microsoft.Network/dnsZones/TXT@2018-05-01' = [for record in txtRecords: {
  parent: zone
  name: record.name
  properties: {
    TTL: ttl
    // One TXT record per value — Azure DNS merges them into a single record-set
    // at `name`. (A value longer than 255 chars would itself need chunking into
    // string[]; callers pre-split when that rare case arises.)
    TXTRecords: [for v in record.values: {
      value: [
        v
      ]
    }]
  }
}]

// ── Outputs ───────────────────────────────────────────────────

@description('Resource ID of the DNS zone.')
output zoneId string = zone.id

@description('Azure-assigned nameservers. Point your registrar NS records at these four values.')
output nameServers array = zone.properties.nameServers
