Auth0 Portal Configuration Checklist
1. API (Resource Server)
Create under Applications > APIs:

Setting	Value
Name	RVS API
Identifier (Audience)	https://api.rvserviceflow.com
Signing Algorithm	RS256
Token Expiration	3600 (1 hour)
Token Expiration for Browser Flows	3600
Allow Offline Access	Enabled (for refresh tokens)
RBAC Settings > Enable RBAC	Enabled
RBAC Settings > Add Permissions in the Access Token	Enabled
2. API Permissions
Define all 21 permissions on the API under APIs > RVS API > Permissions:

Permission	Description
service-requests:read	View service request details
service-requests:search	Search / filter service requests
service-requests:create	Create a service request from the dealer dashboard
service-requests:update	Update service request (status, notes, category)
service-requests:update-service-event	Update Section 10A fields (repair action, parts, labor)
service-requests:delete	Delete a service request
attachments:read	View / download attachments
attachments:upload	Upload an attachment
attachments:delete	Delete an attachment
dealerships:read	View dealership details
dealerships:update	Update dealership settings
locations:read	View location details and list
locations:create	Create a new physical location
locations:update	Update location settings
analytics:read	View service request analytics
tenants:config:read	View tenant configuration
tenants:config:create	Bootstrap tenant configuration
tenants:config:update	Update tenant configuration
lookups:read	Read lookup sets
platform:tenants:manage	Create / disable / enable tenants
platform:lookups:manage	Create / update global lookup sets
3. Roles
Create under User Management > Roles. Assign the permissions per the matrix below:

Role	Permissions
platform:admin	All 21 permissions
dealer:corporate-admin	All except platform:tenants:manage, platform:lookups:manage (19 permissions)
dealer:owner	Same as dealer:corporate-admin (19 permissions)
dealer:regional-manager	service-requests:read, search, create, update, update-service-event, delete + attachments:read, upload, delete + dealerships:read + locations:read, update + analytics:read + lookups:read (14 permissions)
dealer:manager	Same as dealer:regional-manager (14 permissions)
dealer:advisor	service-requests:read, search, create, update + attachments:read, upload + dealerships:read + locations:read + lookups:read (9 permissions)
dealer:technician	service-requests:read, search, update-service-event + attachments:read, upload + dealerships:read + locations:read + lookups:read (8 permissions)
dealer:readonly	service-requests:read, search + attachments:read + dealerships:read + locations:read + analytics:read + lookups:read (7 permissions)
4. Application (SPA)
Create under Applications > Applications (type: Single Page Application):

Setting	Value
Name	RVS Blazor Client
Application Type	Single Page Application
Allowed Callback URLs	https://localhost:7116/authentication/login-callback, https://app.rvserviceflow.com/authentication/login-callback
Allowed Logout URLs	https://localhost:7116, https://app.rvserviceflow.com
Allowed Web Origins	https://localhost:7116, https://app.rvserviceflow.com
Token Endpoint Auth Method	None (SPA)
Refresh Token Rotation	Enabled
Refresh Token Expiration	30 days (rolling)
5. Custom Claims Namespace
All custom claims use the namespace: https://rvserviceflow.com/

6. Post-Login Action (Actions > Flows > Login)
Create a Login Action named e.g. Enrich Access Token that injects these custom claims:

Claim	Namespace + Key	Source (MVP)	Source (Orgs)
Tenant ID	https://rvserviceflow.com/tenantId	event.user.app_metadata.tenantId	event.organization.id
Org Name	https://rvserviceflow.com/orgName	event.user.app_metadata.orgName	event.organization.display_name
Roles	https://rvserviceflow.com/roles	event.authorization.roles	event.authorization.roles
User ID	https://rvserviceflow.com/userId	event.user.user_id	event.user.user_id
Location IDs	https://rvserviceflow.com/locationIds	event.user.app_metadata.locationIds	event.user.app_metadata.locationIds
Region Tag	https://rvserviceflow.com/regionTag	event.user.app_metadata.regionTag	event.user.app_metadata.regionTag
Sample Action code (MVP mode):

7. User app_metadata Structure (per user)
Set on each user under User Management > Users > {user} > app_metadata:

tenantId — required for all dealer staff; maps to Cosmos partition key
orgName — display name for the corporation
locationIds — array of location IDs the user can access; omit or leave empty for corporate-wide roles (dealer:corporate-admin, dealer:owner)
regionTag — optional; only for dealer:regional-manager users
8. Token Settings
Setting	Value
Access Token Lifetime	3600 seconds (1 hour)
Refresh Token Lifetime	2,592,000 seconds (30 days, rolling)
ID Token Lifetime	36000 seconds (default)
Client storage	Memory-only or sessionStorage — never localStorage
9. Blazor Client Configuration Fix Needed
Current mismatch detected — the Blazor WASM dev config points to a different Auth0 tenant than the API:

Config	API	Blazor WASM
Authority/Domain	https://dev-rvserviceflow.us.auth0.com/	https://dev-2jhzz8xmjggh26pm.us.auth0.com/
Audience	https://api.rvserviceflow.com	https://api.benefetch.com (dev)
Both apps must use the same Auth0 tenant domain and audience. Ensure the Blazor client's Authority and Audience match:

Authority: https://dev-rvserviceflow.us.auth0.com/
Audience: https://api.rvserviceflow.com
10. Scopes (requested by Blazor SPA at login)
The SPA should request these scopes in the authorization request:

Scope	Purpose
openid	Required for OIDC
profile	User name/avatar
email	User email
offline_access	Refresh token support
The permissions are not requested as scopes — they're automatically included in the access token because "Add Permissions in Access Token" is enabled on the API.

11. Summary of Auth0 Portal Sections to Touch
APIs — Create RVS API with audience, RS256, RBAC enabled, permissions defined
Applications — Create SPA app with callback/logout/origin URLs
Roles — Create 8 roles with correct permission assignments
Actions > Login Flow — Deploy the Post-Login Action for custom claims
Users — Set app_metadata (tenantId, locationIds, regionTag) per user
Organizations — Deferred to commercialization; MVP uses app_metadata for tenant scoping