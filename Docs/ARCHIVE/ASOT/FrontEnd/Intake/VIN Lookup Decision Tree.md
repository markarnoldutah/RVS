HandleLookup()
│
├─ _submitted = true → _form.Validate()
│  └─ Form invalid? (VIN empty, bad format via ClientVinValidator)
│     └─ YES → return (show field-level validation error) ── STOP
│
├─ _isLookingUp = true → StateHasChanged() (show spinner)
│
├─ TRY: IntakeApi.DecodeVinAsync(slug, vin)
│  │
│  │  ┌─────────────── IntakeApiClient.DecodeVinAsync ───────────────┐
│  │  │ HTTP GET api/intake/{slug}/decode-vin/{vin}                  │
│  │  │                                                              │
│  │  │  ┌──────── IntakeController.DecodeVin ────────┐              │
│  │  │  │ _vinDecoderService.DecodeVinAsync(vin, ct) │              │
│  │  │  │                                            │              │
│  │  │  │  🟢 Returns VinDecoderResult               │              │
│  │  │  │     → Controller: Ok(VinDecodeResponseDto) │ → 200       │
│  │  │  │                                            │              │
│  │  │  │  🟡 Returns null                           │              │
│  │  │  │     → Controller: NotFound()               │ → 404       │
│  │  │  │                                            │              │
│  │  │  │  🔴 Throws HttpRequestException            │              │
│  │  │  │     → ExceptionHandlingMiddleware           │              │
│  │  │  │       HttpRequestException matches _ =>    │ → 500       │
│  │  │  └────────────────────────────────────────────┘              │
│  │  │                                                              │
│  │  │ Status 404? → return null                                    │
│  │  │ Status 200? → EnsureSuccessStatusCode() OK → deserialize DTO │
│  │  │ Status 500? → EnsureSuccessStatusCode() THROWS               │
│  │  │              HttpRequestException                            │
│  │  └──────────────────────────────────────────────────────────────┘
│  │
│  ├─ decoded is NOT null ─────────────── 🟢 HAPPY PATH
│  │  ├─ State.Manufacturer/Model/Year = decoded values
│  │  ├─ State.VinLookupSucceeded = true
│  │  ├─ State.NotifyAndPersistAsync()
│  │  └─ OnNext.InvokeAsync() → advance to Step 4
│  │
│  └─ decoded IS null ─────────────────── 🟡 SAD PATH 1 (VIN not found)
│     └─ _lookupFailed = true
│
├─ CATCH HttpRequestException ─────────── 🔴 SAD PATH 2 (API failure — your case)
│  └─ _lookupFailed = true
│
├─ CATCH TaskCanceledException ─────────── 🟠 SAD PATH 3 (timeout/cancel)
│  └─ _lookupFailed = true
│
└─ FINALLY
   ├─ _isLookingUp = false
   └─ StateHasChanged()
         │
         └─ _lookupFailed == true?
            ├─ Show MudAlert (Severity.Warning) with error message
            ├─ Show "Continue Anyway" outlined button → HandleProceedAnyway()
            └─ Primary button text changes to "Try Lookup Again"