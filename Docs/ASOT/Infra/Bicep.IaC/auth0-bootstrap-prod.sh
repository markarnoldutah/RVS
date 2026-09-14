#!/usr/bin/env bash
# One-time bootstrap of the RVS production Auth0 tenant.
#
# Prereqs (see Docs/ASOT/Auth0/Auth0-Portal-Configuration-Checklist.md for the
# manual equivalent of everything this script does):
#   - A new Auth0 tenant already created in the dashboard (tenant creation has
#     no Management API endpoint, so it can't be scripted).
#   - A throwaway Machine-to-Machine application in that tenant, authorized
#     against "Auth0 Management API" with scopes: create/read:resource_servers,
#     create/read/update:roles, create:role_members, create/read/update/delete:clients,
#     read/update:connections, create/read/update:actions, read/update:triggers,
#     create/read/update:users.
#   - That app's Domain / Client ID / Client Secret stored in Key Vault as
#     Auth0Bootstrap--Domain / Auth0Bootstrap--ClientId / Auth0Bootstrap--ClientSecret.
#   - `az` (logged in, with Key Vault Secrets Officer on the target vault),
#     `curl`, `jq`, and (only if creating a user) `openssl`.
#
# What it does:
#   1. Creates the RVS API resource server with all 21 permissions, RBAC +
#      permissions-in-access-token enabled.
#   2. Creates the 7 in-scope roles (dealer:technician is archived, not created)
#      and assigns each its permission set.
#   3. Creates the "RVS Blazor Client" SPA app, scoped to the prod Manager URL only.
#   4. Enables that SPA on the default Username-Password-Authentication connection.
#   5. Creates, deploys, and binds the "Enrich Access Token" post-login Action.
#   6. Optionally creates one production user (see AUTH0_BOOTSTRAP_USER_* below).
#   7. Writes Auth0--Domain/Audience/ClientId/ClientSecret/TokenUrl/AuthorizationUrl
#      into Key Vault (these feed RVS.API's Swagger OAuth2 login helper only —
#      see Program.cs's BearerSecuritySchemeTransformer).
#   8. Deletes the bootstrap M2M app and its Key Vault secrets so no standing
#      superuser credential is left behind.
#
# What it deliberately does NOT do:
#   - Create the Auth0 tenant itself.
#   - Update RVS.Blazor.Manager/wwwroot/appsettings.Production.json (Authority/
#     ClientId) — that's a reviewable repo change, printed at the end for you
#     to apply, not something this script should silently edit.
#   - Redeploy the Manager SWA.
#
# Usage:
#   AUTH0_BOOTSTRAP_KEYVAULT=kv-rvs-prod-wus3 ./auth0-bootstrap-prod.sh
#
# Optional env vars to also create the first production user in the same run:
#   AUTH0_BOOTSTRAP_USER_EMAIL, AUTH0_BOOTSTRAP_USER_TENANT_ID,
#   AUTH0_BOOTSTRAP_USER_ORG_NAME, AUTH0_BOOTSTRAP_USER_LOCATION_IDS (comma-separated),
#   AUTH0_BOOTSTRAP_USER_ROLE (default dealer:manager)
#
# Set AUTH0_BOOTSTRAP_CLEANUP=false to keep the bootstrap M2M app/secrets
# around after the run (e.g. if you want to re-run this script again soon).

set -euo pipefail

KEYVAULT_NAME="${AUTH0_BOOTSTRAP_KEYVAULT:-kv-rvs-prod-wus3}"
API_AUDIENCE="https://api.rvserviceflow.com"
SPA_APP_NAME="RVS Blazor Client"
MANAGER_ORIGIN="https://manager.rvserviceflow.com"
ACTION_NAME="Enrich Access Token"

BOOTSTRAP_USER_EMAIL="${AUTH0_BOOTSTRAP_USER_EMAIL:-}"
BOOTSTRAP_USER_TENANT_ID="${AUTH0_BOOTSTRAP_USER_TENANT_ID:-}"
BOOTSTRAP_USER_ORG_NAME="${AUTH0_BOOTSTRAP_USER_ORG_NAME:-}"
BOOTSTRAP_USER_LOCATION_IDS="${AUTH0_BOOTSTRAP_USER_LOCATION_IDS:-}"
BOOTSTRAP_USER_ROLE="${AUTH0_BOOTSTRAP_USER_ROLE:-dealer:manager}"

DELETE_BOOTSTRAP_APP="${AUTH0_BOOTSTRAP_CLEANUP:-true}"

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }
command -v az >/dev/null || { echo "az CLI is required" >&2; exit 1; }

kv_get() { az keyvault secret show --vault-name "$KEYVAULT_NAME" --name "$1" --query value -o tsv; }
kv_set() { az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "$1" --value "$2" >/dev/null; }

echo "==> Reading bootstrap credentials from $KEYVAULT_NAME"
DOMAIN="$(kv_get 'Auth0Bootstrap--Domain')"
BOOT_CLIENT_ID="$(kv_get 'Auth0Bootstrap--ClientId')"
BOOT_CLIENT_SECRET="$(kv_get 'Auth0Bootstrap--ClientSecret')"
DOMAIN="${DOMAIN%/}"
MGMT_BASE="https://${DOMAIN}/api/v2"

echo "==> Requesting Management API token"
TOKEN_RESPONSE="$(curl -sS -X POST "https://${DOMAIN}/oauth/token" \
  -H 'content-type: application/json' \
  -d "$(jq -n --arg cid "$BOOT_CLIENT_ID" --arg cs "$BOOT_CLIENT_SECRET" --arg aud "https://${DOMAIN}/api/v2/" \
        '{grant_type:"client_credentials", client_id:$cid, client_secret:$cs, audience:$aud}')")"
TOKEN="$(echo "$TOKEN_RESPONSE" | jq -r '.access_token // empty')"
if [[ -z "$TOKEN" ]]; then
  echo "Failed to get Management API token:" >&2
  echo "$TOKEN_RESPONSE" >&2
  exit 1
fi

api() {
  # api METHOD PATH [JSON_BODY]
  local method="$1" path="$2" body="${3:-}"
  local args=(-sS -X "$method" "${MGMT_BASE}${path}" -H "authorization: Bearer ${TOKEN}" -H "content-type: application/json")
  [[ -n "$body" ]] && args+=(-d "$body")
  local resp status
  resp="$(curl "${args[@]}" -w $'\n%{http_code}')"
  status="$(echo "$resp" | tail -n1)"
  resp="$(echo "$resp" | sed '$d')"
  if [[ "$status" -ge 300 ]]; then
    echo "Auth0 API error ($method $path): HTTP $status" >&2
    echo "$resp" >&2
    exit 1
  fi
  echo "$resp"
}

# ── 1. Resource Server (API) ─────────────────────────────────────────────────
echo "==> Creating resource server $API_AUDIENCE"
SCOPES_JSON=$(jq -n '[
  {value:"service-requests:read", description:"View service request details"},
  {value:"service-requests:search", description:"Search / filter service requests"},
  {value:"service-requests:create", description:"Create a service request from the dealer dashboard"},
  {value:"service-requests:update", description:"Update service request (status, notes, category)"},
  {value:"service-requests:update-service-event", description:"Technician repair-outcome fields (archived scope)"},
  {value:"service-requests:delete", description:"Delete a service request"},
  {value:"attachments:read", description:"View / download attachments"},
  {value:"attachments:upload", description:"Upload an attachment"},
  {value:"attachments:delete", description:"Delete an attachment"},
  {value:"dealerships:read", description:"View dealership details"},
  {value:"dealerships:update", description:"Update dealership settings"},
  {value:"locations:read", description:"View location details and list"},
  {value:"locations:create", description:"Create a new physical location"},
  {value:"locations:update", description:"Update location settings"},
  {value:"analytics:read", description:"View service request analytics (archived scope)"},
  {value:"tenants:config:read", description:"View tenant configuration"},
  {value:"tenants:config:create", description:"Bootstrap tenant configuration"},
  {value:"tenants:config:update", description:"Update tenant configuration"},
  {value:"lookups:read", description:"Read lookup sets"},
  {value:"platform:tenants:manage", description:"Create / disable / enable tenants"},
  {value:"platform:lookups:manage", description:"Create / update global lookup sets"}
]')
RS_PAYLOAD=$(jq -n --arg id "$API_AUDIENCE" --argjson scopes "$SCOPES_JSON" '{
  name: "RVS API",
  identifier: $id,
  signing_alg: "RS256",
  token_lifetime: 3600,
  token_lifetime_for_web: 3600,
  enforce_policies: true,
  token_dialect: "access_token_authz",
  scopes: $scopes
}')
api POST /resource-servers "$RS_PAYLOAD" >/dev/null

# ── 2. Roles ──────────────────────────────────────────────────────────────────
declare -A ROLE_PERMS=(
  ["platform:admin"]="service-requests:read service-requests:search service-requests:create service-requests:update service-requests:update-service-event service-requests:delete attachments:read attachments:upload attachments:delete dealerships:read dealerships:update locations:read locations:create locations:update analytics:read tenants:config:read tenants:config:create tenants:config:update lookups:read platform:tenants:manage platform:lookups:manage"
  ["dealer:corporate-admin"]="service-requests:read service-requests:search service-requests:create service-requests:update service-requests:update-service-event service-requests:delete attachments:read attachments:upload attachments:delete dealerships:read dealerships:update locations:read locations:create locations:update analytics:read tenants:config:read tenants:config:create tenants:config:update lookups:read"
  ["dealer:owner"]="service-requests:read service-requests:search service-requests:create service-requests:update service-requests:update-service-event service-requests:delete attachments:read attachments:upload attachments:delete dealerships:read dealerships:update locations:read locations:create locations:update analytics:read tenants:config:read tenants:config:create tenants:config:update lookups:read"
  ["dealer:regional-manager"]="service-requests:read service-requests:search service-requests:create service-requests:update service-requests:update-service-event service-requests:delete attachments:read attachments:upload attachments:delete dealerships:read locations:read locations:update analytics:read lookups:read"
  ["dealer:manager"]="service-requests:read service-requests:search service-requests:create service-requests:update service-requests:update-service-event service-requests:delete attachments:read attachments:upload attachments:delete dealerships:read locations:read locations:update analytics:read lookups:read"
  ["dealer:advisor"]="service-requests:read service-requests:search service-requests:create service-requests:update attachments:read attachments:upload dealerships:read locations:read lookups:read"
  ["dealer:readonly"]="service-requests:read service-requests:search attachments:read dealerships:read locations:read analytics:read lookups:read"
)

declare -A ROLE_IDS
for role in "${!ROLE_PERMS[@]}"; do
  echo "==> Creating role: $role"
  ROLE_RESP=$(api POST /roles "$(jq -n --arg name "$role" '{name:$name, description:$name}')")
  ROLE_ID=$(echo "$ROLE_RESP" | jq -r '.id')
  ROLE_IDS["$role"]="$ROLE_ID"

  PERMS_JSON=$(jq -n --arg rsid "$API_AUDIENCE" --arg perms "${ROLE_PERMS[$role]}" \
    '$perms | split(" ") | map({resource_server_identifier:$rsid, permission_name:.})')
  api POST "/roles/${ROLE_ID}/permissions" "$(jq -n --argjson permissions "$PERMS_JSON" '{permissions:$permissions}')" >/dev/null
done

# ── 3. SPA application ────────────────────────────────────────────────────────
echo "==> Creating SPA application: $SPA_APP_NAME"
CLIENT_PAYLOAD=$(jq -n --arg name "$SPA_APP_NAME" --arg origin "$MANAGER_ORIGIN" '{
  name: $name,
  app_type: "spa",
  token_endpoint_auth_method: "none",
  oidc_conformant: true,
  callbacks: [($origin + "/authentication/login-callback")],
  allowed_logout_urls: [$origin, ($origin + "/authentication/logout-callback")],
  web_origins: [$origin],
  allowed_origins: [$origin],
  grant_types: ["authorization_code","refresh_token","implicit"],
  refresh_token: {
    rotation_type: "rotating",
    expiration_type: "expiring",
    token_lifetime: 2592000,
    idle_token_lifetime: 604800
  }
}')
CLIENT_RESP=$(api POST /clients "$CLIENT_PAYLOAD")
CLIENT_ID=$(echo "$CLIENT_RESP" | jq -r '.client_id')
CLIENT_SECRET=$(echo "$CLIENT_RESP" | jq -r '.client_secret')
echo "    client_id: $CLIENT_ID"

# ── 4. Enable SPA on default DB connection ───────────────────────────────────
echo "==> Enabling $SPA_APP_NAME on Username-Password-Authentication"
CONN_RESP=$(api GET "/connections?name=Username-Password-Authentication")
CONN_ID=$(echo "$CONN_RESP" | jq -r '.[0].id')
EXISTING_CLIENTS=$(echo "$CONN_RESP" | jq -c '.[0].enabled_clients // []')
NEW_CLIENTS=$(jq -n --argjson existing "$EXISTING_CLIENTS" --arg cid "$CLIENT_ID" '$existing + [$cid] | unique')
api PATCH "/connections/${CONN_ID}" "$(jq -n --argjson enabled_clients "$NEW_CLIENTS" '{enabled_clients:$enabled_clients}')" >/dev/null

# ── 5. Post-Login Action ──────────────────────────────────────────────────────
echo "==> Creating Post-Login Action: $ACTION_NAME"
ACTION_CODE=$(cat <<'JSCODE'
exports.onExecutePostLogin = async (event, api) => {
  const namespace = "https://rvserviceflow.com/";
  const md = event.user.app_metadata;

  if (md?.tenantId) {
    api.accessToken.setCustomClaim(`${namespace}tenantId`, md.tenantId);
  }
  if (md?.orgName) {
    api.accessToken.setCustomClaim(`${namespace}orgName`, md.orgName);
  }
  if (event.authorization?.roles?.length) {
    api.accessToken.setCustomClaim(`${namespace}roles`, event.authorization.roles);
  }
  api.accessToken.setCustomClaim(`${namespace}userId`, event.user.user_id);

  if (md?.locationIds) {
    api.accessToken.setCustomClaim(`${namespace}locationIds`, md.locationIds);
  }
  if (md?.regionTag) {
    api.accessToken.setCustomClaim(`${namespace}regionTag`, md.regionTag);
  }
};
JSCODE
)
ACTION_PAYLOAD=$(jq -n --arg name "$ACTION_NAME" --arg code "$ACTION_CODE" '{
  name: $name,
  supported_triggers: [{id:"post-login", version:"v3"}],
  code: $code,
  runtime: "node18"
}')
ACTION_RESP=$(api POST /actions/actions "$ACTION_PAYLOAD")
ACTION_ID=$(echo "$ACTION_RESP" | jq -r '.id')
api POST "/actions/actions/${ACTION_ID}/deploy" "{}" >/dev/null

echo "==> Binding action into the Login flow"
EXISTING_BINDINGS=$(api GET "/actions/triggers/post-login/bindings" | jq -c '.bindings // []')
NEW_BINDING=$(jq -n --arg id "$ACTION_ID" --arg name "$ACTION_NAME" '{ref:{type:"action_id", value:$id}, display_name:$name}')
NEW_BINDINGS=$(jq -n --argjson existing "$EXISTING_BINDINGS" --argjson new "$NEW_BINDING" '$existing + [$new]')
api PATCH "/actions/triggers/post-login/bindings" "$(jq -n --argjson bindings "$NEW_BINDINGS" '{bindings:$bindings}')" >/dev/null

# ── 6. Optional: first production user ───────────────────────────────────────
if [[ -n "$BOOTSTRAP_USER_EMAIL" ]]; then
  command -v openssl >/dev/null || { echo "openssl is required to create a user" >&2; exit 1; }
  echo "==> Creating user: $BOOTSTRAP_USER_EMAIL"
  TEMP_PASSWORD="$(openssl rand -base64 24)"
  LOCATION_IDS_JSON=$(jq -n --arg csv "$BOOTSTRAP_USER_LOCATION_IDS" '$csv | split(",") | map(select(length>0))')
  USER_PAYLOAD=$(jq -n \
    --arg email "$BOOTSTRAP_USER_EMAIL" \
    --arg password "$TEMP_PASSWORD" \
    --arg tenantId "$BOOTSTRAP_USER_TENANT_ID" \
    --arg orgName "$BOOTSTRAP_USER_ORG_NAME" \
    --argjson locationIds "$LOCATION_IDS_JSON" \
    '{
      connection: "Username-Password-Authentication",
      email: $email,
      password: $password,
      email_verified: false,
      app_metadata: {tenantId:$tenantId, orgName:$orgName, locationIds:$locationIds}
    }')
  USER_RESP=$(api POST /users "$USER_PAYLOAD")
  USER_ID=$(echo "$USER_RESP" | jq -r '.user_id')
  ROLE_ID_FOR_USER="${ROLE_IDS[$BOOTSTRAP_USER_ROLE]:-}"
  if [[ -z "$ROLE_ID_FOR_USER" ]]; then
    echo "Unknown role $BOOTSTRAP_USER_ROLE — skipping role assignment" >&2
  else
    api POST "/users/${USER_ID}/roles" "$(jq -n --arg rid "$ROLE_ID_FOR_USER" '{roles:[$rid]}')" >/dev/null
  fi
  echo "    Temporary password (share with $BOOTSTRAP_USER_EMAIL out of band, then have them reset it): $TEMP_PASSWORD"
fi

# ── 7. Final Key Vault secrets ────────────────────────────────────────────────
echo "==> Writing production Auth0 secrets to $KEYVAULT_NAME"
kv_set "Auth0--Domain" "$DOMAIN"
kv_set "Auth0--Audience" "$API_AUDIENCE"
kv_set "Auth0--ClientId" "$CLIENT_ID"
kv_set "Auth0--ClientSecret" "$CLIENT_SECRET"
kv_set "Auth0--TokenUrl" "https://${DOMAIN}/oauth/token"
kv_set "Auth0--AuthorizationUrl" "https://${DOMAIN}/authorize"

# ── 8. Cleanup ────────────────────────────────────────────────────────────────
if [[ "$DELETE_BOOTSTRAP_APP" == "true" ]]; then
  echo "==> Deleting bootstrap M2M application"
  api DELETE "/clients/${BOOT_CLIENT_ID}" >/dev/null || echo "    Could not delete bootstrap app automatically — delete it by hand in the dashboard." >&2
  az keyvault secret delete --vault-name "$KEYVAULT_NAME" --name "Auth0Bootstrap--Domain" >/dev/null 2>&1 || true
  az keyvault secret delete --vault-name "$KEYVAULT_NAME" --name "Auth0Bootstrap--ClientId" >/dev/null 2>&1 || true
  az keyvault secret delete --vault-name "$KEYVAULT_NAME" --name "Auth0Bootstrap--ClientSecret" >/dev/null 2>&1 || true
fi

echo ""
echo "==> Done. Remaining manual steps:"
echo "    1. Update RVS.Blazor.Manager/wwwroot/appsettings.Production.json:"
echo "         Auth0.Authority = https://${DOMAIN}/"
echo "         Auth0.ClientId  = ${CLIENT_ID}"
echo "    2. Rebuild/redeploy the Manager SWA so the new appsettings ships (rides the next staging build → prod promote)."
echo "    3. Spot-check the resource server, roles, action binding, and (if created) the user in the Auth0 dashboard."
