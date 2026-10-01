#!/usr/bin/env bash
# Captures an Auth0 tenant's configuration as normalized, secret-redacted JSON so
# it can be committed and diffed. Read-only: it never writes to the tenant.
#
# Usage:
#   ./auth0-export.sh <tenant> [output-dir]
#
#   <tenant>      a tenants/<tenant>.env file — today "shared"; later dev, staging, prod
#   output-dir    default: snapshots/<tenant>
#
# Output (ids and timestamps that change on every read are stripped; keys sorted):
#   tenant-settings.json, resource-servers.json, roles.json (with permissions),
#   clients.json, client-grants.json, connections.json,
#   actions/<name>.json + actions/<name>.js (deployed code), triggers/post-login.json,
#   prompts.json, attack-protection.json
#
# Optional sections that fail with 403/404 (missing scope, or a plan without the
# feature) are skipped with a warning; required sections abort the run.
#
# Management API scopes this script uses:
#   read:tenant_settings read:resource_servers read:roles read:clients
#   read:client_grants read:connections read:actions read:triggers
#   read:prompts read:attack_protection

set -euo pipefail
source "$(dirname "$0")/lib.sh"

[[ $# -ge 1 ]] || die "usage: $0 <tenant> [output-dir]"
TENANT="$1"
require_tools
load_tenant "$TENANT"
OUT="${2:-$AUTH0_SCRIPTS_DIR/snapshots/$TENANT}"

mgmt_login

mkdir -p "$OUT/actions" "$OUT/triggers"
rm -f "$OUT"/actions/* "$OUT"/triggers/*

# write FILE JQ_FILTER [JQ_ARGS...]  — normalizes stdin through the filter, redacts, sorts keys.
write() {
  local file="$1" filter="$2"
  shift 2
  jq -S "$@" "$filter | $AUTH0_REDACT_JQ" >"$OUT/$file"
  echo "    $file" >&2
}

STRIP_TIMES='walk(if type == "object" then del(.created_at, .updated_at, .deployed_at, .last_login, .built_at) else . end)'

log "Tenant settings"
api GET /tenants/settings | write tenant-settings.json "$STRIP_TIMES"

log "Resource servers"
api_get_all /resource-servers resource_servers \
  | write resource-servers.json "map(select(.is_system != true)) | map(del(.id)) | sort_by(.identifier) | $STRIP_TIMES"

log "Roles and permissions"
ROLES="$(api_get_all /roles roles)"
ROLES_OUT='[]'
for rid in $(jq -r '.[].id' <<<"$ROLES"); do
  perms="$(api_get_all "/roles/${rid}/permissions" permissions)"
  ROLES_OUT="$(jq -c --arg rid "$rid" --argjson perms "$perms" --argjson roles "$ROLES" '
    . + [($roles[] | select(.id == $rid) | {name, description}) + {
      permissions: ($perms | map({resource_server_identifier, permission_name})
                           | sort_by(.resource_server_identifier, .permission_name))
    }]' <<<"$ROLES_OUT")"
done
write roles.json 'sort_by(.name)' <<<"$ROLES_OUT"

log "Clients"
# Without read:client_keys Auth0 omits client_secret and signing keys from the
# response (asking to exclude them by name is itself a 403); the redact filter is
# the backstop if that scope is ever granted.
CLIENTS="$(api_get_all /clients clients)"
write clients.json "map(select(.global != true)) | sort_by(.name) | $STRIP_TIMES" <<<"$CLIENTS"
# client_id -> name map, used to make grants and connections readable.
CLIENT_NAMES="$(jq -c 'map({key: .client_id, value: .name}) | from_entries' <<<"$CLIENTS")"

log "Client grants"
api_get_all /client-grants client_grants \
  | write client-grants.json \
    'map(del(.id) | . + {client_name: ($names[.client_id] // null)} | .scope |= sort) | sort_by(.client_name, .audience)' \
    --argjson names "$CLIENT_NAMES"

log "Connections"
CONNECTIONS="$(api_get_all /connections connections)"
ENABLED='{}'
for cid in $(jq -r '.[].id' <<<"$CONNECTIONS"); do
  ids="$(connection_client_ids "$cid")"
  ENABLED="$(jq -c --arg cid "$cid" --argjson ids "$ids" '. + {($cid): $ids}' <<<"$ENABLED")"
done
write connections.json \
  "map(.enabled_client_names = ((\$enabled[.id] // []) | map(\$names[.] // .) | sort)
       | del(.id, .enabled_clients))
   | sort_by(.name) | $STRIP_TIMES" \
  --argjson names "$CLIENT_NAMES" --argjson enabled "$ENABLED" <<<"$CONNECTIONS"

log "Actions"
ACTIONS="$(api_get_all /actions/actions actions false)"
for aid in $(jq -r '.[].id' <<<"$ACTIONS"); do
  action="$(jq -c --arg id "$aid" '.[] | select(.id == $id)' <<<"$ACTIONS")"
  slug="$(jq -r '.name | ascii_downcase | gsub("[^a-z0-9]+"; "-") | gsub("^-|-$"; "")' <<<"$action")"
  deployed_code="$(jq -r '.deployed_version.code // empty' <<<"$action")"
  draft_code="$(jq -r '.code // empty' <<<"$action")"
  if [[ -n "$deployed_code" ]]; then
    printf '%s\n' "$deployed_code" >"$OUT/actions/$slug.js"
  else
    warn "action '$slug' has never been deployed; capturing its draft code"
    printf '%s\n' "$draft_code" >"$OUT/actions/$slug.js"
  fi
  if [[ -n "$deployed_code" && "$deployed_code" != "$draft_code" ]]; then
    warn "action '$slug' has undeployed draft changes; draft saved as $slug.draft.js"
    printf '%s\n' "$draft_code" >"$OUT/actions/$slug.draft.js"
  fi
  jq -S "{name, runtime, status, supported_triggers, dependencies, secrets: ((.secrets // []) | map({name})),
          deployed: (.deployed_version != null), all_changes_deployed} | $AUTH0_REDACT_JQ" \
    <<<"$action" >"$OUT/actions/$slug.json"
  echo "    actions/$slug.{json,js}" >&2
done

log "Post-login trigger bindings"
api GET /actions/triggers/post-login/bindings \
  | jq -S '[.bindings[] | {display_name, action_name: .action.name}]' >"$OUT/triggers/post-login.json"
echo "    triggers/post-login.json" >&2

log "Optional sections"
if prompts="$(api GET /prompts 2>/dev/null)"; then
  write prompts.json '.' <<<"$prompts"
else
  warn "prompts skipped (missing read:prompts?)"
fi

ap='{}'
ap_ok=true
for section in brute-force-protection suspicious-ip-throttling breached-password-detection; do
  if body="$(api GET "/attack-protection/$section" 2>/dev/null)"; then
    ap="$(jq -c --arg s "$section" --argjson b "$body" '. + {($s): $b}' <<<"$ap")"
  else
    ap_ok=false
  fi
done
if [[ "$ap" != '{}' ]]; then write attack-protection.json '.' <<<"$ap"; fi
$ap_ok || warn "some attack-protection sections skipped (missing read:attack_protection?)"

jq -n -S --arg domain "$AUTH0_DOMAIN" --arg tenant "$TENANT" '{tenant: $tenant, domain: $domain}' >"$OUT/_tenant.json"

log "Done. Snapshot written to $OUT"
