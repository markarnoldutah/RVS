// ──────────────────────────────────────────────────────────────
// Module: DNS Zone Contributor RBAC (zone-scoped)
// ──────────────────────────────────────────────────────────────
// Grants the built-in "DNS Zone Contributor" role on a single
// DNS zone (NOT the resource group) to one or more principals.
//
// Use case: the staging deployment principal must be able to
// write CNAME/A/TXT record sets into prod-owned zones
// (rvserviceflow.com, rvintake.com) without holding RG-wide
// Contributor on rg-rvs-prod-westus3. Granting at the zone
// scope is the principle-of-least-privilege approach.
//
// Built-in role id: befefa01-2a29-4197-83a8-272ff33ce314
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/networking#dns-zone-contributor
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

@description('Name of the existing DNS zone to scope the role assignment to (e.g. rvintake.com).')
param zoneName string

@description('Object IDs of the principals (typically GitHub Actions federated service principals) that need write access to record sets in this zone.')
param principalIds string[]

@description('Principal type for the role assignment. Use ServicePrincipal for managed identities, app registrations, and federated SPs.')
@allowed([
  'ServicePrincipal'
  'User'
  'Group'
])
param principalType string = 'ServicePrincipal'

var dnsZoneContributorRoleId = 'befefa01-2a29-4197-83a8-272ff33ce314'

resource zone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: zoneName
}

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in principalIds: {
  name: guid(zone.id, principalId, dnsZoneContributorRoleId)
  scope: zone
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dnsZoneContributorRoleId)
    principalId: principalId
    principalType: principalType
  }
}]
