/**
* Handler that will be called during the execution of a PostLogin flow.
*
* --- AUTH0 ACTIONS TEMPLATE https://github.com/auth0/opensource-marketplace/blob/main/templates/role-creation-POST_LOGIN ---
*
* @param {Event} event - Details about the user and the context in which they are logging in.
* @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
*/
exports.onExecutePostLogin = async (event, api) => {

    const namespace = 'https://rvserviceflow.com/';
    const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

    // Using template literals for cleaner strings
    const tenantIdClaim = `${namespace}tenantId`;
    const locationIdsClaim = `${namespace}locationIds`;
    const orgNameClaim = `${namespace}orgName`;
    const regionTagClaim = `${namespace}regionTag`;
    // const userIdClaim = `${namespace}userId`;

    // Extract values using helper functions
    const tenantId = findTenantId();
    const roles = findRoles();
    const locationIds = findLocationIds();
    const orgName = findOrgName();
    const regionTag = findRegionTag();

    // Set claims: access_token
    api.accessToken.setCustomClaim(roleClaim, roles);
    api.accessToken.setCustomClaim(tenantIdClaim, tenantId);
    api.accessToken.setCustomClaim(locationIdsClaim, locationIds);
    api.accessToken.setCustomClaim(orgNameClaim, orgName);
    api.accessToken.setCustomClaim(regionTagClaim, regionTag);
    // api.accessToken.setCustomClaim(userIdClaim, event.user.user_id);

    // Set claims: id_token
    api.idToken.setCustomClaim(tenantIdClaim, tenantId)
    api.idToken.setCustomClaim(locationIdsClaim, locationIds)
    api.idToken.setCustomClaim(orgNameClaim, orgName)
    api.idToken.setCustomClaim(regionTagClaim, regionTag)
    // api.idToken.setCustomClaim(userIdClaim, event.user.user_id)

    // --- Helper Functions ---

    function findTenantId() {
        const tenantId = event.user.app_metadata?.tenantId;
        if (!tenantId) {
            return api.access.deny("No tenantId found. You do not appear to be authorized for any tenants.");
        }
        return tenantId;
    }

    function findRoles() {
        const roles = event.authorization?.roles;
        if (!Array.isArray(roles) || roles.length === 0) {
            return api.access.deny("No roles found. You do not appear to be authorized for any roles.");
        }
        return roles;
    }

    function findLocationIds() {
        return event.user.app_metadata?.locationIds ?? [];
    }

    function findOrgName() {
        const orgName = event.user.app_metadata?.orgName;
        if (!orgName) {
            return api.access.deny("No OrgName found. You do not appear to be authorized for any organizations.");
        }
        return orgName;
    }

    function findRegionTag() {
        return event.user.app_metadata?.regionTag ?? '';
    }
};

/**
 * Handler that will be invoked when this action is resuming after an external redirect. If your
 * onExecutePostLogin function does not perform a redirect, this function can be safely ignored.
 *
 * @param {Event} event - Details about the user and the context in which they are logging in.
 * @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
 */
// exports.onContinuePostLogin = async (event, api) => {
// };
