# Validation Not Running on Component Initialization - FIXED (v2)

## ? **Problem**

When `EncounterDetailsSection` loaded with default values (e.g., auto-selecting the only available location or first visit type), the validation message wouldn't update until the user manually changed the field, even though the field already had a valid value.

**User Experience:**
- Location shows "Office 1" (auto-selected, only option)
- Visit Type shows first option (auto-selected)
- Warning message says: "Please complete: Location, Visit Type"
- User confused: "But both fields ARE selected!"
- User has to **change** the field to trigger validation

---

## ?? **Root Cause Analysis**

### **Issue 1: Two-Way Binding Didn't Trigger Updates**

The original implementation used property setters with `@bind-SelectedOption`. The setter only fires when the **user changes** the selection, not when defaults are applied programmatically.

### **Issue 2: Defaults Applied Before Lookups Loaded**

`OnParametersSet` fires multiple times - first when `Locations` is empty (still loading), then after lookups load.

### **Issue 3: No Default Visit Type**

Only Location had default logic. Visit Type never auto-selected.

---

## ? **Solution**

### **1. Use Explicit Event Handlers Instead of Two-Way Binding**

Changed from `@bind-SelectedOption` to explicit `SelectedOption` + `SelectedOptionChanged` handlers.

### **2. Apply Defaults When Lookups Are Available**

Track whether defaults have been applied and apply them when lookup data is available. Now sets defaults for **both** Location (if only one) and Visit Type (first option).

### **3. Notify Parent After Defaults Applied**

Triggers `OnEncounterChanged` callback after defaults are set.

---

## ?? **Testing**

### **Test Case 1: Single Location Practice**

1. Configure practice with only 1 location
2. Create new patient, fill in demographics
3. **Expected:**
   - ? Location auto-selected to only option
   - ? Visit Type auto-selected to first option
   - ? No warning message (all fields filled)
   - ? Button enabled immediately

---

## ? **Status**

**Status:** ? FIXED

**Root Cause:** Two-way binding property setters don't fire for programmatic/default values

**Solution:** Use explicit change handlers + apply defaults when lookups load + track defaults applied
