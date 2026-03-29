  /**
 * Handler that will be called during the execution of a PostLogin flow.
 *
 * --- AUTH0 ACTIONS TEMPLATE https://github.com/auth0/opensource-marketplace/blob/main/templates/role-creation-POST_LOGIN ---
 *
 * @param {Event} event - Details about the user and the context in which they are logging in.
 * @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
 */
exports.onExecutePostLogin = async (event, api) => {

    const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    
    const namespace = 'https://rvserviceflow.com/'
    const tenantIdClaim = namespace + 'tenantId'
    const locationIdsClaim = namespace + 'locationIds'
    const orgNameClaim = namespace + 'orgName'
    const userIdClaim = namespace + 'userId'
    const regionTagClaim = namespace + 'regionTag'

    let tenantId = findTenantId()
    let locationIds = findLocationIds()
    let orgName = findOrgName()
    let regionTag = findRegionTag()

    // Access token — used by the API (server-side JWT bearer validation)
    api.accessToken.setCustomClaim(tenantIdClaim, tenantId)
    api.accessToken.setCustomClaim(locationIdsClaim, locationIds)
    api.accessToken.setCustomClaim(orgNameClaim, orgName)
    api.accessToken.setCustomClaim(userIdClaim, event.user.user_id)
    api.accessToken.setCustomClaim(regionTagClaim, regionTag)

    // ID token — used by Blazor WebAssembly (AuthenticationState.User / ClaimsPrincipal)
    api.idToken.setCustomClaim(tenantIdClaim, tenantId)
    api.idToken.setCustomClaim(locationIdsClaim, locationIds)
    api.idToken.setCustomClaim(orgNameClaim, orgName)
    api.idToken.setCustomClaim(userIdClaim, event.user.user_id)
    api.idToken.setCustomClaim(regionTagClaim, regionTag)


    function findTenantId() {
        // let tenantId = event.user.app_metadata.tenantId
        let tenantId = event.organization?.id;

        if (!tenantId) {
        return api.access.deny("No tenantId found in metadata. You do not appear to be authorized for any tenants.");
        }

        return tenantId;
    }

    function findLocationIds() {
        let locationIds = event.user.app_metadata.locationIds
        if (locationIds && Array.isArray(locationIds) && locationIds.length > 0) {
            return locationIds
        } else {
            api.access.deny("No locationIds found in metadata.  You do not appear to be authorized for any locations.")
        }
    }

    function findOrgName() {
        let orgName = event.user.app_metadata.orgName
        if (orgName) {
            return orgName
        } else {
            api.access.deny("No OrgName found in metadata.  You do not appear to be authorized for any organizations.")
        }
    }

    function findRegionTag() {
        let regionTag = event.user.app_metadata.regionTag
        if (regionTag) {
            return regionTag
        } else {
            // non-blocking, optional region tag
            // api.access.deny("No RegionTag found in metadata.  You do not appear to be authorized for any regions.")
            return 'NA'
        }
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
