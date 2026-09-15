# Shared helpers for the Auth0 configuration scripts. Source it; don't run it.
#
# Requires az (logged in, able to read secrets in the tenant's Management vault),
# curl and jq. Written for macOS's stock bash 3.2: no associative arrays, no mapfile.

AUTH0_SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() { echo "error: $*" >&2; exit 1; }
log() { echo "==> $*" >&2; }
warn() { echo "    warning: $*" >&2; }

require_tools() {
  local t
  for t in az curl jq; do
    command -v "$t" >/dev/null || die "$t is required"
  done
}

# load_tenant NAME
# Sources tenants/NAME.env and checks the variables every script needs.
load_tenant() {
  local name="$1" file="$AUTH0_SCRIPTS_DIR/tenants/$1.env" v
  if [[ ! -f "$file" ]]; then
    die "no tenant config at $file (available: $(cd "$AUTH0_SCRIPTS_DIR/tenants" && ls ./*.env 2>/dev/null | sed 's|^\./||; s|\.env$||' | tr '\n' ' '))"
  fi
  # shellcheck source=/dev/null
  source "$file"
  for v in AUTH0_TENANT_NAME AUTH0_DOMAIN AUTH0_MGMT_KEYVAULT AUTH0_MGMT_SECRET_PREFIX; do
    [[ -n "${!v:-}" ]] || die "$v is not set in $file"
  done
  [[ "$AUTH0_TENANT_NAME" == "$name" ]] || die "$file sets AUTH0_TENANT_NAME=$AUTH0_TENANT_NAME, expected $name"
}

kv_get() {
  az keyvault secret show --vault-name "$1" --name "$2" --query value -o tsv
}

# mgmt_login
# Exchanges the tenant's Management API M2M credentials (from Key Vault) for an
# access token. The token goes into a 0600 header file rather than onto curl's
# command line, so it never shows up in `ps`.
mgmt_login() {
  local prefix="$AUTH0_MGMT_SECRET_PREFIX" vault="$AUTH0_MGMT_KEYVAULT"
  local vault_domain client_id client_secret resp token

  log "Reading ${prefix}--* credentials from $vault"
  vault_domain="$(kv_get "$vault" "${prefix}--Domain")" || die "cannot read ${prefix}--Domain from $vault"
  client_id="$(kv_get "$vault" "${prefix}--ClientId")" || die "cannot read ${prefix}--ClientId from $vault"
  client_secret="$(kv_get "$vault" "${prefix}--ClientSecret")" || die "cannot read ${prefix}--ClientSecret from $vault"

  # Guard against a tenant file and a vault that point at different tenants.
  vault_domain="${vault_domain#https://}"
  vault_domain="${vault_domain%/}"
  [[ "$vault_domain" == "$AUTH0_DOMAIN" ]] \
    || die "$vault holds credentials for $vault_domain, but tenant '$AUTH0_TENANT_NAME' is $AUTH0_DOMAIN"

  log "Requesting Management API token for $AUTH0_DOMAIN"
  resp="$(jq -n --arg cid "$client_id" --arg cs "$client_secret" --arg aud "https://${AUTH0_DOMAIN}/api/v2/" \
      '{grant_type:"client_credentials", client_id:$cid, client_secret:$cs, audience:$aud}' \
    | curl -sS -X POST "https://${AUTH0_DOMAIN}/oauth/token" -H 'content-type: application/json' --data-binary @-)"
  token="$(jq -r '.access_token // empty' <<<"$resp")"
  [[ -n "$token" ]] || die "token request failed: $(jq -c '{error, error_description}' <<<"$resp" 2>/dev/null || echo "$resp")"

  AUTH0_WORK_DIR="$(mktemp -d)"
  chmod 700 "$AUTH0_WORK_DIR"
  trap 'rm -rf "$AUTH0_WORK_DIR"' EXIT
  AUTH0_HEADER_FILE="$AUTH0_WORK_DIR/headers"
  printf 'authorization: Bearer %s\ncontent-type: application/json\n' "$token" >"$AUTH0_HEADER_FILE"
  chmod 600 "$AUTH0_HEADER_FILE"
}

# api METHOD PATH [JSON_BODY]
# Prints the response body. Retries on 429. Exits non-zero on any other non-2xx:
# 3 for 403 (missing scope, or a feature the plan doesn't have), 4 for 404, 1 otherwise.
api() {
  local method="$1" path="$2" body="${3:-}" out status attempt=0
  out="$(mktemp "$AUTH0_WORK_DIR/resp.XXXXXX")"
  while :; do
    if [[ -n "$body" ]]; then
      status="$(curl -sS -o "$out" -w '%{http_code}' -X "$method" "https://${AUTH0_DOMAIN}/api/v2${path}" \
        -H "@$AUTH0_HEADER_FILE" --data-binary @- <<<"$body")"
    else
      status="$(curl -sS -o "$out" -w '%{http_code}' -X "$method" "https://${AUTH0_DOMAIN}/api/v2${path}" \
        -H "@$AUTH0_HEADER_FILE")"
    fi
    if [[ "$status" == "429" && $attempt -lt 5 ]]; then
      attempt=$((attempt + 1))
      sleep $((attempt * 2))
      continue
    fi
    break
  done
  if [[ "$status" -lt 200 || "$status" -ge 300 ]]; then
    echo "Auth0 API $method $path -> HTTP $status: $(jq -c '{error, message, errorCode}' "$out" 2>/dev/null || cat "$out")" >&2
    rm -f "$out"
    case "$status" in
      403) exit 3 ;;
      404) exit 4 ;;
      *) exit 1 ;;
    esac
  fi
  cat "$out"
  rm -f "$out"
}

# api_get_or_empty PATH
# Like `api GET PATH`, except a 404 prints nothing and succeeds.
api_get_or_empty() {
  local rc=0 body err
  err="$(mktemp "$AUTH0_WORK_DIR/err.XXXXXX")"
  body="$(api GET "$1" 2>"$err")" || rc=$?
  if [[ $rc -ne 4 ]]; then cat "$err" >&2; fi
  rm -f "$err"
  if [[ $rc -eq 4 ]]; then return 0; fi
  [[ $rc -eq 0 ]] || exit $rc
  printf '%s\n' "$body"
}

# connection_client_ids CONNECTION_ID
# Prints a JSON array of the client_ids enabled on a connection. The
# enabled_clients field on the connection object is deprecated and comes back null.
connection_client_ids() {
  local ids='[]' from="" resp
  while :; do
    resp="$(api GET "/connections/$1/clients?take=100${from:+&from=$(jq -rn --arg f "$from" '$f | @uri')}")" || exit $?
    ids="$(jq -c --argjson more "$(jq -c '[.clients[].client_id]' <<<"$resp")" '. + $more' <<<"$ids")"
    from="$(jq -r '.next // empty' <<<"$resp")"
    [[ -n "$from" ]] || break
  done
  echo "$ids"
}

# api_get_all PATH KEY [TOTALS]
# Follows page/per_page pagination on a list endpoint and prints one merged array.
# PATH may already carry a query string. Pass TOTALS=false for endpoints that
# reject include_totals (the Actions API does).
api_get_all() {
  local path="$1" key="$2" totals_param="&include_totals=true" sep="?" page=0 per_page=50 acc='[]' resp items total
  [[ "${3:-true}" == "false" ]] && totals_param=""
  [[ "$path" == *\?* ]] && sep="&"
  while :; do
    resp="$(api GET "${path}${sep}page=${page}&per_page=${per_page}${totals_param}")" || exit $?
    items="$(jq -c --arg k "$key" 'if type == "array" then . else .[$k] // [] end' <<<"$resp")"
    total="$(jq -r 'if type == "array" then -1 else .total // -1 end' <<<"$resp")"
    acc="$(jq -c --argjson more "$items" '. + $more' <<<"$acc")"
    if [[ "$(jq 'length' <<<"$items")" -lt $per_page ]]; then break; fi
    if [[ "$total" -ge 0 && "$(jq 'length' <<<"$acc")" -ge "$total" ]]; then break; fi
    page=$((page + 1))
  done
  echo "$acc"
}

# jq filter that masks secret-bearing values anywhere in a document. Key names
# are matched whole (or by a _secret / _password suffix) so settings such as
# password_policy stay readable; key material is dropped outright.
AUTH0_REDACT_JQ='walk(if type == "object" then
  del(.signing_keys, .encryption_key)
  | with_entries(
      if (.key | test("^(secret|client_secret|password|private_key|api_key|client_key|key|access_token|refresh_token)$|_secret$|_password$"; "i"))
         and (.value | type) != "object" and (.value | type) != "array"
      then .value = "<redacted>" else . end)
  else . end)'
