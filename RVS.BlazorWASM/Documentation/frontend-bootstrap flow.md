Authenticate → then bootstrap using these endpoints:

1. **Authenticate**

* Blazor WASM initializes auth and acquires an access token that includes `tenantId` (and roles/permissions).

2. **Load tenant configuration (bootstrap source-of-truth)**

* Call:

  * `GET /api/config/tenant`
* Backend infers `tenantId` from the token and returns `TenantConfig`, including:

  * practice defaults + startup directory (if you’re projecting it there)
  * encounter settings
  * eligibility settings
  * COB settings
  * UI settings
  * **AccessGate**

3. **Gate check**

* If `tenantConfig.AccessGate.LoginsEnabled == false`:

  * stop bootstrapping
  * show `DisabledMessage` + `SupportContactEmail`
  * do not load further data

4. **Load payer configurations**

* Call:

  * `GET /api/config/payers`
* Returns all `PayerConfig` docs for the tenant:

  * tenant-wide defaults (`PracticeId == null`)
  * per-practice overrides (`PracticeId == <practiceId>`)
* Frontend stores them and resolves effective payer config as:

  * practice override → else tenant default

5. **Load lookup sets**

* Call:

  * `GET /api/lookups`
* Returns lookup sets used for dropdowns, filters, labels, and display mapping.

6. **Select active practice**

* If multiple practices exist:

  * prompt user to choose
* Otherwise:

  * auto-select the only practice
* Store `currentPracticeId` in client state; it drives payer-config resolution and encounter defaults.

7. **App ready**

* With `TenantConfig`, `PayerConfigs`, `Lookups`, and active practice selected, the UI can safely render routes and begin normal workflows (patients, encounters, eligibility checks, COB decisions).
