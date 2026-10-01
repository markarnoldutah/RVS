> **Superseded for new users (issue #563).** Create tenants and users with the provisioning tool, `/admin` in the Manager app; it does steps 3–5 below and writes the Cosmos documents in the same submission. One-time setup is §5 of `Auth0/Auth0-Portal-Configuration-Checklist.md`. This page is kept as the record of how Jay's account was set up by hand.

> **The Manager hostname below is retired (#634).** It read `manager.rvserviceflow.com` when these steps were run; the Manager app now lives at `manager.rvintake.com`, and the old host no longer resolves. The URLs are left as written because this is a record of what was done, not instructions to repeat — but do not copy them into Auth0 today. Manager origins are now managed in `Infra/Auth0/tenants/shared.env` (`AUTH0_MANAGER_ORIGINS`) and applied with `auth0-apply.sh`, not edited by hand in the dashboard. The API identifier `https://api.rvserviceflow.com` and the claim namespace `https://rvserviceflow.com/` are unchanged and still correct.

1. Allow the prod manager URLs
Applications → Applications, then open the manager app. Its Client ID is CBdytt7GQhJSejyVwSrCZZRYUkLMXdiY.
On the Settings tab, add each value to the end of the existing comma-separated list:
Allowed Callback URLs: https://manager.rvserviceflow.com/authentication/login-callback
Allowed Logout URLs: https://manager.rvserviceflow.com/authentication/logout-callback
Allowed Web Origins: https://manager.rvserviceflow.com
Allowed Origins (CORS): https://manager.rvserviceflow.com. The manager app refreshes its sign-in by calling Auth0's /oauth/token from the browser, so this one matters.
Click Save Changes at the bottom.
2. Confirm the API still sends permissions
Applications → APIs, then open https://api.rvserviceflow.com.
On the Settings tab, check that Enable RBAC and Add Permissions in the Access Token are both on. Every manager API call checks the permissions claim.
On the Permissions tab, check that the permissions exist, for example service-requests:read and locations:read. They should already be there, since staging uses them.
3. Create Jay's user
User Management → Users → + Create User.
Enter Jay's email, choose the same connection staging users use (probably Username-Password-Authentication), set a temporary password, and click Create.
4. Set Jay's app_metadata
On Jay's user page, Details tab, scroll to Metadata → app_metadata.
Paste this and click Save:

{
  "tenantId": "ten_nova_rv",
  "orgName": "Nova RV Services",
  "locationIds": ["loc_nova_hurricane"]
}
tenantId must match what I seeded into prod Cosmos exactly.
orgName is required: the login Action rejects anyone without it.
locationIds isn't checked by the API today, but it scopes Jay correctly for when it is.
5. Give Jay a role
On Jay's user page, Roles tab, then Assign Roles, and pick dealer:manager.
This is required, because the login Action rejects users with no role.
Also check that the role actually carries the permissions from step 2, under User Management → Roles → dealer:manager → Permissions. Authorization depends on the permissions, not the role name. The simplest safe choice is to give Jay the same role your working staging account has.
6. Check the login Action is active
Actions → Triggers → post-login.
Make sure the Action that adds metadata to the access token is in the flow, between Start and Complete.
It should set https://rvserviceflow.com/tenantId and https://rvserviceflow.com/orgName. If it's missing, the API will return 401 for Jay.
7. Test after B11 deploys the manager app
Open https://manager.rvserviceflow.com in a private window and sign in as Jay.
You should land on Nova's service board (still empty) with no 401 errors.
If sign-in shows a callback error, recheck step 1. If it says "No tenantId / No roles / No OrgName found", recheck steps 4–5.
The only other Auth0 file in the repo is an archived copy of that Action, so the live one could differ. Once Jay has signed in, I can confirm his tenant claim works by checking the prod API logs for his requests.