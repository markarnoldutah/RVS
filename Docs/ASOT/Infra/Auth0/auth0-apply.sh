#!/usr/bin/env bash
# Brings the RVS-owned part of an Auth0 tenant in line with baseline/.
#
# Usage:
#   ./auth0-apply.sh <tenant>                  # plan: print differences, change nothing
#   ./auth0-apply.sh <tenant> --apply          # plan, confirm by typing the domain, apply
#   ./auth0-apply.sh <tenant> --apply --yes    # skip the confirmation (automation)
#
#   <tenant>  a tenants/<tenant>.env file — today "shared"; later dev, staging, prod
#
# Exit codes: 0 no differences (or applied), 2 differences found in plan mode, 1 error.
#
# What it manages, from baseline/:
#   resource-server.json   the RVS API (audience from AUTH0_API_IDENTIFIER) and its permissions
#   roles.json             RVS roles and their permissions on that API
#   clients/<name>.json    applications listed in AUTH0_CLIENTS; URL lists come from the
#                          tenant file (see each file's _urls), plus the application's
#                          grant to the RVS API (_grant) and its connections (_connections)
#   actions/<name>.json    Actions, deployed from the matching <name>.js, and their bindings
#
# What it leaves alone: everything else in the tenant (other products' APIs and apps,
# Postman / API Explorer apps, tenant-wide settings, attack protection, users). It never
# deletes a resource. Inside the resources it manages it is authoritative: permissions
# missing from the baseline are removed from RVS roles, grants and the RVS API.
#
# Management API scopes:
#   plan   read:resource_servers read:roles read:clients read:client_grants
#          read:connections read:actions read:triggers
#   apply  plan scopes + create:resource_servers update:resource_servers create:roles
#          update:roles create:clients update:clients create:client_grants
#          update:client_grants update:connections create:actions update:actions
#          update:triggers

set -euo pipefail
source "$(dirname "$0")/lib.sh"

usage() { die "usage: $0 <tenant> [--apply [--yes]]"; }
[[ $# -ge 1 ]] || usage
TENANT="$1"
shift
WANT_APPLY=false
ASSUME_YES=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --apply) WANT_APPLY=true ;;
    --yes) ASSUME_YES=true ;;
    *) usage ;;
  esac
  shift
done

require_tools
load_tenant "$TENANT"
for v in AUTH0_API_IDENTIFIER AUTH0_CLIENTS; do
  [[ -n "${!v:-}" ]] || die "$v is not set in tenants/$TENANT.env"
done

BASE="$AUTH0_SCRIPTS_DIR/baseline"
API_ENC="$(jq -rn --arg s "$AUTH0_API_IDENTIFIER" '$s | @uri')"
RS_DESIRED="$(jq -c . "$BASE/resource-server.json")"
DESIRED_SCOPE_VALUES="$(jq -c '[.scopes[].value] | sort' <<<"$RS_DESIRED")"

# Arrays compare as sets: Auth0 doesn't preserve the order of most of them.
NORMALIZE_JQ='walk(if type == "array" then sort else . end)'
# Reduces a live object to the keys the baseline declares, recursively, so fields
# RVS doesn't manage never show up as differences.
PROJECT_JQ='def project($d):
  if ($d | type) == "object" and type == "object"
  then . as $live | reduce ($d | keys[]) as $k ({}; .[$k] = ($live[$k] | project($d[$k])))
  else . end;'

APPLY=false
CHANGES=0
CLIENT_IDS='{}'

change() { CHANGES=$((CHANGES + 1)); }

project() { jq -c --argjson d "$2" "$PROJECT_JQ project(\$d)" <<<"$1"; }

# differs LABEL LIVE_JSON BASELINE_JSON
# Prints "=" when equal; otherwise prints a unified diff, counts a change, returns 0.
differs() {
  local label="$1" a="$AUTH0_WORK_DIR/a.json" b="$AUTH0_WORK_DIR/b.json"
  jq -S "$NORMALIZE_JQ" <<<"$2" >"$a"
  jq -S "$NORMALIZE_JQ" <<<"$3" >"$b"
  if cmp -s "$a" "$b"; then
    echo "  = $label"
    return 1
  fi
  echo "  ~ $label"
  diff -u "$a" "$b" | tail -n +3 | sed 's/^/      /' || true
  change
  return 0
}

# differs_text LABEL LIVE_TEXT BASELINE_TEXT — as differs, for source code.
differs_text() {
  local label="$1" a="$AUTH0_WORK_DIR/a.txt" b="$AUTH0_WORK_DIR/b.txt"
  printf '%s\n' "$2" >"$a"
  printf '%s\n' "$3" >"$b"
  if cmp -s "$a" "$b"; then
    echo "  = $label"
    return 1
  fi
  echo "  ~ $label"
  diff -u "$a" "$b" | tail -n +3 | sed 's/^/      /' || true
  change
  return 0
}

# render_client SPEC_JSON
# Strips _-prefixed control keys and fills each _urls field from the tenant file:
# {"from": "ENV_VAR", "format": "{}/path"} expands to one URL per word of $ENV_VAR.
render_client() {
  local spec="$1" out field from fmt words list
  out="$(jq -c 'with_entries(select(.key | startswith("_") | not))' <<<"$spec")"
  for field in $(jq -r '(._urls // {}) | keys[]' <<<"$spec"); do
    from="$(jq -r --arg f "$field" '._urls[$f].from' <<<"$spec")"
    fmt="$(jq -r --arg f "$field" '._urls[$f].format' <<<"$spec")"
    words="${!from:-}"
    [[ -n "$words" ]] || die "$from is not set in tenants/$TENANT.env (needed by $(jq -r .name <<<"$spec"))"
    set -f
    # shellcheck disable=SC2086
    list="$(printf '%s\n' $words | jq -R . | jq -sc --arg fmt "$fmt" 'map(. as $u | $fmt | split("{}") | join($u))')"
    set +f
    out="$(jq -c --arg f "$field" --argjson v "$list" '.[$f] = $v' <<<"$out")"
  done
  echo "$out"
}

# ── Resource server ────────────────────────────────────────────────────────────

sync_resource_server_start() {
  log "Resource server $AUTH0_API_IDENTIFIER"
  RS_LIVE="$(api_get_or_empty "/resource-servers/$API_ENC")"
  RS_ID=""
  RS_PRUNE=false
  if [[ -z "$RS_LIVE" ]]; then
    echo "  + create resource server with $(jq '.scopes | length' <<<"$RS_DESIRED") permissions"
    change
    if $APPLY; then
      RS_ID="$(api POST /resource-servers "$(jq -c --arg id "$AUTH0_API_IDENTIFIER" '. + {identifier: $id}' <<<"$RS_DESIRED")" | jq -r .id)"
    fi
    return 0
  fi
  RS_ID="$(jq -r .id <<<"$RS_LIVE")"
  if differs "resource server" "$(project "$RS_LIVE" "$RS_DESIRED")" "$RS_DESIRED"; then
    if $APPLY; then
      # Keep permissions the baseline drops until roles and grants stop using them;
      # sync_resource_server_finish removes them.
      api PATCH "/resource-servers/$RS_ID" "$(jq -c --argjson live "$RS_LIVE" '
        .scopes as $want
        | .scopes = ($want + [($live.scopes // [])[] | . as $s | select([$want[].value] | index($s.value) | not)])' \
        <<<"$RS_DESIRED")" >/dev/null
      RS_PRUNE=true
    fi
  fi
}

sync_resource_server_finish() {
  if $RS_PRUNE; then
    log "Removing permissions the baseline dropped from $AUTH0_API_IDENTIFIER"
    api PATCH "/resource-servers/$RS_ID" "$(jq -c '{scopes}' <<<"$RS_DESIRED")" >/dev/null
  fi
}

# ── Roles ──────────────────────────────────────────────────────────────────────

permissions_body() {
  jq -nc --arg rs "$AUTH0_API_IDENTIFIER" --argjson names "$1" \
    '{permissions: [$names[] | {resource_server_identifier: $rs, permission_name: .}]}'
}

sync_roles() {
  log "Roles"
  local n i role name matches live_role rid desired_perms live_perms add remove
  LIVE_ROLES="$(api_get_all /roles roles)"
  n="$(jq length "$BASE/roles.json")"
  for ((i = 0; i < n; i++)); do
    role="$(jq -c ".[$i]" "$BASE/roles.json")"
    name="$(jq -r .name <<<"$role")"
    desired_perms="$(jq -c '.permissions | sort' <<<"$role")"
    matches="$(jq -c --arg n "$name" '[.[] | select(.name == $n)]' <<<"$LIVE_ROLES")"
    [[ "$(jq length <<<"$matches")" -le 1 ]] || die "more than one role named $name"

    if [[ "$(jq length <<<"$matches")" -eq 0 ]]; then
      echo "  + create role $name with $(jq length <<<"$desired_perms") permissions"
      change
      live_perms='[]'
      rid=""
      if $APPLY; then
        rid="$(api POST /roles "$(jq -c '{name, description}' <<<"$role")" | jq -r .id)"
      fi
    else
      live_role="$(jq -c '.[0]' <<<"$matches")"
      rid="$(jq -r .id <<<"$live_role")"
      live_perms="$(api_get_all "/roles/$rid/permissions" permissions \
        | jq -c --arg rs "$AUTH0_API_IDENTIFIER" '[.[] | select(.resource_server_identifier == $rs) | .permission_name] | sort')"
      if ! differs "role $name" \
          "$(jq -c --argjson p "$live_perms" '{description, permissions: $p}' <<<"$live_role")" \
          "$(jq -c '{description, permissions}' <<<"$role")"; then
        continue
      fi
      if $APPLY && [[ "$(jq -r .description <<<"$live_role")" != "$(jq -r .description <<<"$role")" ]]; then
        api PATCH "/roles/$rid" "$(jq -c '{description}' <<<"$role")" >/dev/null
      fi
    fi

    if $APPLY; then
      add="$(jq -nc --argjson want "$desired_perms" --argjson have "$live_perms" '$want - $have')"
      remove="$(jq -nc --argjson want "$desired_perms" --argjson have "$live_perms" '$have - $want')"
      if [[ "$add" != "[]" ]]; then
        api POST "/roles/$rid/permissions" "$(permissions_body "$add")" >/dev/null
      fi
      if [[ "$remove" != "[]" ]]; then
        api DELETE "/roles/$rid/permissions" "$(permissions_body "$remove")" >/dev/null
      fi
    fi
  done
}

# ── Applications ───────────────────────────────────────────────────────────────

sync_clients() {
  log "Applications"
  local key file desired name matches live cid
  LIVE_CLIENTS="$(api_get_all /clients clients)"
  for key in $AUTH0_CLIENTS; do
    file="$BASE/clients/$key.json"
    [[ -f "$file" ]] || die "no baseline for application '$key' at $file"
    desired="$(render_client "$(jq -c . "$file")")"
    name="$(jq -r .name <<<"$desired")"
    matches="$(jq -c --arg n "$name" '[.[] | select(.name == $n)]' <<<"$LIVE_CLIENTS")"
    cid=""
    case "$(jq length <<<"$matches")" in
      0)
        echo "  + create application $name"
        change
        if $APPLY; then
          cid="$(api POST /clients "$desired" | jq -r .client_id)"
        fi
        ;;
      1)
        live="$(jq -c '.[0]' <<<"$matches")"
        cid="$(jq -r .client_id <<<"$live")"
        if differs "application $name ($cid)" "$(project "$live" "$desired")" "$desired"; then
          if $APPLY; then
            api PATCH "/clients/$cid" "$desired" >/dev/null
          fi
        fi
        ;;
      *) die "more than one application named '$name'" ;;
    esac
    CLIENT_IDS="$(jq -c --arg k "$key" --arg v "$cid" '. + {($k): $v}' <<<"$CLIENT_IDS")"
  done
}

sync_grants() {
  log "Application grants to $AUTH0_API_IDENTIFIER"
  local key file grant name cid subject want matches live body
  for key in $AUTH0_CLIENTS; do
    file="$BASE/clients/$key.json"
    grant="$(jq -c '._grant // empty' "$file")"
    [[ -n "$grant" ]] || continue
    name="$(jq -r .name "$file")"
    cid="$(jq -r --arg k "$key" '.[$k] // ""' <<<"$CLIENT_IDS")"
    subject="$(jq -r .subject_type <<<"$grant")"
    want="$(jq -c --argjson all "$DESIRED_SCOPE_VALUES" 'if .scope == "all" then $all else .scope end | sort' <<<"$grant")"
    body="$(jq -nc --argjson s "$want" '{scope: $s}')"

    if [[ -z "$cid" ]]; then
      echo "  + grant $name $subject access (after the application is created)"
      change
      continue
    fi
    matches="$(api_get_all "/client-grants?client_id=$cid&audience=$API_ENC" client_grants \
      | jq -c --arg st "$subject" '[.[] | select((.subject_type // "client") == $st)]')"
    if [[ "$(jq length <<<"$matches")" -eq 0 ]]; then
      echo "  + grant $name $subject access with $(jq length <<<"$want") permissions"
      change
      if $APPLY; then
        api POST /client-grants "$(jq -c --arg c "$cid" --arg a "$AUTH0_API_IDENTIFIER" --arg st "$subject" \
          '. + {client_id: $c, audience: $a, subject_type: $st}' <<<"$body")" >/dev/null
      fi
    else
      live="$(jq -c '.[0]' <<<"$matches")"
      if differs "grant $name ($subject access)" "$(jq -c '{scope}' <<<"$live")" "$body"; then
        if $APPLY; then
          api PATCH "/client-grants/$(jq -r .id <<<"$live")" "$body" >/dev/null
        fi
      fi
    fi
  done
}

sync_connections() {
  log "Connections"
  local key file name cid conn conn_id
  LIVE_CONNECTIONS="$(api_get_all /connections connections)"
  for key in $AUTH0_CLIENTS; do
    file="$BASE/clients/$key.json"
    name="$(jq -r .name "$file")"
    cid="$(jq -r --arg k "$key" '.[$k] // ""' <<<"$CLIENT_IDS")"
    for conn in $(jq -r '(._connections // [])[]' "$file"); do
      conn_id="$(jq -r --arg n "$conn" '.[] | select(.name == $n) | .id' <<<"$LIVE_CONNECTIONS")"
      if [[ -z "$conn_id" ]]; then
        warn "connection $conn does not exist in this tenant; $name is not enabled on it"
        continue
      fi
      if [[ -z "$cid" ]]; then
        echo "  + enable $name on $conn (after the application is created)"
        change
        continue
      fi
      if jq -e --arg c "$cid" 'index($c) != null' <<<"$(connection_client_ids "$conn_id")" >/dev/null; then
        echo "  = $name enabled on $conn"
      else
        echo "  + enable $name on $conn"
        change
        if $APPLY; then
          api PATCH "/connections/$conn_id/clients" "$(jq -nc --arg c "$cid" '[{client_id: $c, status: true}]')" >/dev/null
        fi
      fi
    done
  done
}

# ── Actions ────────────────────────────────────────────────────────────────────

deploy_action() {
  local aid="$1" status="" i
  for ((i = 0; i < 30; i++)); do
    status="$(api GET "/actions/actions/$aid" | jq -r .status)"
    case "$status" in
      built) break ;;
      failed) die "action $aid failed to build; see the Auth0 dashboard" ;;
    esac
    sleep 2
  done
  [[ "$status" == "built" ]] || die "action $aid did not finish building (status: $status)"
  api POST "/actions/actions/$aid/deploy" '{}' >/dev/null
}

sync_actions() {
  log "Actions"
  local spec_file spec name code_file desired matches live aid changed trigger bindings
  LIVE_ACTIONS="$(api_get_all /actions/actions actions false)"
  for spec_file in "$BASE"/actions/*.json; do
    spec="$(jq -c . "$spec_file")"
    name="$(jq -r .name <<<"$spec")"
    code_file="${spec_file%.json}.js"
    [[ -f "$code_file" ]] || die "missing action source $code_file"
    desired="$(jq -c --rawfile code "$code_file" '{code: $code, runtime, supported_triggers, dependencies}' <<<"$spec")"
    matches="$(jq -c --arg n "$name" '[.[] | select(.name == $n)]' <<<"$LIVE_ACTIONS")"
    [[ "$(jq length <<<"$matches")" -le 1 ]] || die "more than one action named $name"

    if [[ "$(jq length <<<"$matches")" -eq 0 ]]; then
      echo "  + create and deploy action $name"
      change
      if $APPLY; then
        aid="$(api POST /actions/actions "$(jq -c --arg n "$name" '. + {name: $n}' <<<"$desired")" | jq -r .id)"
        deploy_action "$aid"
      fi
    else
      live="$(jq -c '.[0]' <<<"$matches")"
      aid="$(jq -r .id <<<"$live")"
      if [[ "$(jq -r '.code // ""' <<<"$live")" != "$(jq -r '.deployed_version.code // ""' <<<"$live")" ]]; then
        warn "action $name has undeployed edits in the dashboard; --apply replaces them"
      fi
      changed=false
      if differs "action $name: settings" \
          "$(jq -c '{runtime: (.deployed_version.runtime // .runtime),
                     supported_triggers: [.supported_triggers[] | {id, version}],
                     dependencies: (.deployed_version.dependencies // .dependencies // [])}' <<<"$live")" \
          "$(jq -c '{runtime, supported_triggers, dependencies}' <<<"$desired")"; then
        changed=true
      fi
      if differs_text "action $name: deployed code" \
          "$(jq -r '.deployed_version.code // ""' <<<"$live")" "$(jq -r .code <<<"$desired")"; then
        changed=true
      fi
      if $APPLY && $changed; then
        api PATCH "/actions/actions/$aid" "$desired" >/dev/null
        deploy_action "$aid"
      fi
    fi

    for trigger in $(jq -r '(.bind_to // [])[]' <<<"$spec"); do
      bindings="$(api GET "/actions/triggers/$trigger/bindings" | jq -c '.bindings // []')"
      if jq -e --arg n "$name" 'any(.[]; .action.name == $n)' <<<"$bindings" >/dev/null; then
        echo "  = $name bound to $trigger"
      else
        echo "  + bind $name to $trigger, after $(jq length <<<"$bindings") existing binding(s)"
        change
        if $APPLY; then
          api PATCH "/actions/triggers/$trigger/bindings" "$(jq -c --arg n "$name" '{bindings: (
              [.[] | {ref: {type: "action_id", value: .action.id}, display_name}]
              + [{ref: {type: "action_name", value: $n}, display_name: $n}])}' <<<"$bindings")" >/dev/null
        fi
      fi
    done
  done
}

# ── Reporting ──────────────────────────────────────────────────────────────────

check_appsettings() {
  [[ -n "${AUTH0_MANAGER_APPSETTINGS:-}" && -n "${AUTH0_MANAGER_CLIENT:-}" ]] || return 0
  log "Manager appsettings (checked, not edited)"
  local root cid f authority client audience
  root="$(cd "$AUTH0_SCRIPTS_DIR" && git rev-parse --show-toplevel)"
  cid="$(jq -r --arg k "$AUTH0_MANAGER_CLIENT" '.[$k] // ""' <<<"$CLIENT_IDS")"
  for f in $AUTH0_MANAGER_APPSETTINGS; do
    if [[ ! -f "$root/$f" ]]; then
      warn "$f not found"
      continue
    fi
    authority="$(jq -r '.Auth0.Authority // ""' "$root/$f")"
    client="$(jq -r '.Auth0.ClientId // ""' "$root/$f")"
    audience="$(jq -r '.Auth0.Audience // ""' "$root/$f")"
    if [[ "$authority" == "https://$AUTH0_DOMAIN/" && "$audience" == "$AUTH0_API_IDENTIFIER" && ( -z "$cid" || "$client" == "$cid" ) ]]; then
      echo "  = $f"
    else
      echo "  ! $f: Authority=$authority ClientId=$client Audience=$audience"
      echo "      expected Authority=https://$AUTH0_DOMAIN/ ClientId=${cid:-<client_id of the new application>} Audience=$AUTH0_API_IDENTIFIER"
    fi
  done
}

report_unmanaged() {
  log "In the tenant but not in the baseline (left as-is)"
  local managed_clients
  managed_clients="$(for key in $AUTH0_CLIENTS; do jq -r .name "$BASE/clients/$key.json"; done | jq -R . | jq -sc .)"
  jq -r --argjson want "$(jq -c '[.[].name]' "$BASE/roles.json")" \
    '[.[].name | select(. as $n | $want | index($n) | not)] | if length > 0 then "  roles:        " + join(", ") else empty end' <<<"$LIVE_ROLES"
  jq -r --argjson want "$managed_clients" \
    '[.[] | select(.global != true) | .name | select(. as $n | $want | index($n) | not)] | if length > 0 then "  applications: " + join(", ") else empty end' <<<"$LIVE_CLIENTS"
  jq -r --argjson want "$(for f in "$BASE"/actions/*.json; do jq -r .name "$f"; done | jq -R . | jq -sc .)" \
    '[.[].name | select(. as $n | $want | index($n) | not)] | if length > 0 then "  actions:      " + join(", ") else empty end' <<<"$LIVE_ACTIONS"
  api_get_all /resource-servers resource_servers | jq -r --arg id "$AUTH0_API_IDENTIFIER" \
    '[.[] | select(.is_system != true and .identifier != $id) | .name] | if length > 0 then "  APIs:         " + join(", ") else empty end'
}

# ── Main ───────────────────────────────────────────────────────────────────────

run() {
  CHANGES=0
  CLIENT_IDS='{}'
  sync_resource_server_start
  sync_roles
  sync_clients
  sync_grants
  sync_connections
  sync_actions
  sync_resource_server_finish
}

mgmt_login

echo "Tenant '$TENANT' ($AUTH0_DOMAIN) compared with baseline/"
run
check_appsettings
report_unmanaged

if [[ $CHANGES -eq 0 ]]; then
  echo
  echo "No differences: the tenant matches the baseline."
  exit 0
fi

if ! $WANT_APPLY; then
  echo
  echo "$CHANGES difference(s). Re-run with --apply to make them."
  exit 2
fi

if ! $ASSUME_YES; then
  [[ -t 0 ]] || die "refusing to apply without a terminal to confirm on; pass --yes"
  printf '\nApply %d change(s) to %s? Type the domain to confirm: ' "$CHANGES" "$AUTH0_DOMAIN"
  read -r answer
  [[ "$answer" == "$AUTH0_DOMAIN" ]] || die "not confirmed; nothing was changed"
fi

echo
echo "Applying to $AUTH0_DOMAIN"
APPLY=true
run
check_appsettings
echo
echo "Done. Run without --apply to confirm the tenant now matches the baseline."
