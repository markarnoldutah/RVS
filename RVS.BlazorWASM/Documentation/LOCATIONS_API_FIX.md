# Locations API Endpoint - Corrected Implementation

## ? **Problems Found**

### **Problem 1: Non-Existent Locations Endpoint**

The `LookupApiService.GetLocationsAsync()` was calling a non-existent endpoint:
```csharp
// ? INCORRECT - This endpoint does not exist
$"api/practices/{practiceId}/locations"
```

**Error:**
```
404 Not Found - /api/practices/{practiceId}/locations
```

### **Problem 2: Incorrect Lookup Deserialization**

All lookup methods (`GetVisitTypesAsync`, `GetPlanTypesAsync`, etc.) were trying to deserialize directly to `List<LookupItemDto>`, but the API returns a `LookupSetDto` wrapper object.

**Error:**
```
System.Text.Json.JsonException: DeserializeUnableToConvertValue, 
System.Collections.Generic.List`1[BF.Domain.DTOs.LookupItemDto]
```

---

## ?? **API Surface Analysis**

### **Available Endpoints**

| Endpoint | Method | Returns | Notes |
|----------|--------|---------|-------|
| `/api/practices` | GET | `List<PracticeSummaryResponseDto>` | Includes nested locations |
| `/api/practices/{id}` | GET | `PracticeDetailResponseDto` | Includes nested locations |
| `/api/lookups/{lookupSetId}` | GET | `LookupSetDto` | **Not** `List<LookupItemDto>` |

### **DTO Structures**

**LookupSetDto (What API returns):**
```json
{
  "category": "VisitType",
  "name": "Visit Types",
  "items": [
    {
      "code": "RoutineVision",
      "name": "Routine Vision Exam",
      "description": "Annual eye exam",
      "sortOrder": 10,
      "isSelectable": true
    }
  ]
}
```

**Expected by Client (WRONG):**
```csharp
// ? Trying to deserialize directly to List<LookupItemDto>
await _httpClient.GetFromJsonAsync<List<LookupItemDto>>("api/lookups/visit-types")
```

---

## ? **Solutions Applied**

### **1. Fixed Locations (GET from Practice Endpoint)**

```csharp
public async Task<List<LocationSummaryResponseDto>> GetLocationsAsync(
    string practiceId,
    CancellationToken cancellationToken = default)
{
    // ? Get practice detail which includes locations
    var practice = await _httpClient.GetFromJsonAsync<PracticeDetailResponseDto>(
        $"api/practices/{practiceId}", cancellationToken);
    
    // Extract locations from nested property
    return practice?.Locations ?? [];
}
```

### **2. Fixed All Lookup Methods (Extract Items from LookupSetDto)**

```csharp
public async Task<List<LookupItemDto>> GetVisitTypesAsync(CancellationToken cancellationToken = default)
{
    // ? Deserialize to LookupSetDto
    var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
        "api/lookups/visit-types", cancellationToken);
    
    // ? Extract Items list
    return lookupSet?.Items.ToList() ?? [];
}

public async Task<List<LookupItemDto>> GetPlanTypesAsync(CancellationToken cancellationToken = default)
{
    var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
        "api/lookups/plan-types", cancellationToken);
    return lookupSet?.Items.ToList() ?? [];
}

public async Task<List<LookupItemDto>> GetRelationshipTypesAsync(CancellationToken cancellationToken = default)
{
    var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
        "api/lookups/relationship-types", cancellationToken);
    return lookupSet?.Items.ToList() ?? [];
}

public async Task<List<LookupItemDto>> GetCobReasonsAsync(CancellationToken cancellationToken = default)
{
    var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
        "api/lookups/cob-reasons", cancellationToken);
    return lookupSet?.Items.ToList() ?? [];
}
```

---

## ?? **Before vs After**

### **Before (BROKEN)**

```csharp
// ? Wrong: Tries to deserialize wrapper object as list
public async Task<List<LookupItemDto>> GetVisitTypesAsync(...)
{
    return await _httpClient.GetFromJsonAsync<List<LookupItemDto>>(
        "api/lookups/visit-types", cancellationToken) ?? [];
}
```

**API Response:**
```json
{
  "category": "VisitType",
  "name": "Visit Types",
  "items": [ ... ]
}
```

**Deserialization:** ? FAILS - Cannot convert object to List

### **After (FIXED)**

```csharp
// ? Correct: Deserialize to wrapper, then extract Items
public async Task<List<LookupItemDto>> GetVisitTypesAsync(...)
{
    var lookupSet = await _httpClient.GetFromJsonAsync<LookupSetDto>(
        "api/lookups/visit-types", cancellationToken);
    return lookupSet?.Items.ToList() ?? [];
}
```

**API Response:**
```json
{
  "category": "VisitType",
  "name": "Visit Types",
  "items": [ ... ]
}
```

**Deserialization:** ? SUCCESS - Gets LookupSetDto, extracts Items property

---

## ?? **Testing**

### **After Fix:**

1. **Restart Blazor WASM app**
2. Navigate to Check-In page: `/practices/{practiceId}/check-in`
3. **Expected:**
   - ? All dropdowns populate correctly
   - ? Visit Types dropdown has values
   - ? Plan Types dropdown has values
   - ? Relationship Types dropdown has values
   - ? COB Reasons dropdown has values
   - ? Locations dropdown has values
   - ? No JSON deserialization errors in console

### **Verify in DevTools**

**Network Tab:**
```
Request:  GET https://localhost:7116/api/lookups/visit-types
Status:   200 OK
Response: 
{
  "category": "VisitType",
  "name": "Visit Types",
  "items": [
    { "code": "RoutineVision", "name": "Routine Vision Exam", ... },
    { "code": "Medical", "name": "Medical Eye Visit", ... }
  ]
}
```

**Client-Side:**
```javascript
// lookupSet = LookupSetDto
// lookupSet.Items = List<LookupItemDto> ?
```

---

## ?? **Files Updated**

### **BF.BlazorWASM\Services\LookupApiService.cs**

**Changes:**
1. ? `GetVisitTypesAsync()` - Deserialize to LookupSetDto, extract Items
2. ? `GetPlanTypesAsync()` - Deserialize to LookupSetDto, extract Items
3. ? `GetRelationshipTypesAsync()` - Deserialize to LookupSetDto, extract Items
4. ? `GetCobReasonsAsync()` - Deserialize to LookupSetDto, extract Items
5. ? `GetLocationsAsync()` - Call practice endpoint, extract Locations

---

## ? **Status**

**Status:** ? ALL FIXED

**Root Causes:**
1. ? Locations: Calling non-existent endpoint ? ? Fixed: Call practice endpoint
2. ? Lookups: Wrong deserialization type ? ? Fixed: Deserialize to LookupSetDto, extract Items

**Impact:** All lookup dropdowns in Check-In page will now populate correctly

**Action Required:** Restart Blazor WASM app and test
