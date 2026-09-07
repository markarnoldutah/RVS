# Intake App — WCAG 2.2 Accessibility Audit & Implementation Plan

**Version:** 1.0
**Date:** April 10, 2026
**Scope:** `RVS.Blazor.Intake` (Blazor WASM PWA), `RVS.UI.Shared` shared components
**Standard:** WCAG 2.2 Level AA conformance
**Approach:** Audit only — no code changes. This document catalogues every finding and prescribes the minimal fix for each.

---

## Companion Documents

| Document | Purpose |
|---|---|
| [RVS_Features_Blazor.Intake_App.md](RVS_Features_Blazor.Intake_App.md) | Intake feature specification |
| [RVS_FrontEnd_Solution.md](../RVS_FrontEnd_Solution.md) | Front-end solution decisions |
| [WCAG 2.2 Specification](https://www.w3.org/TR/WCAG22/) | Normative reference |

---

## Audit Summary

| Severity | Count | Description |
|---|---|---|
| **Critical** | 6 | Blocks assistive-technology users from completing the wizard |
| **Major** | 12 | Significantly degrades the experience for AT users |
| **Minor** | 10 | Best-practice improvements for full AA conformance |
| **Total** | 28 | |

---

## 1. Perceivable (WCAG Principle 1)

### 1.1 Text Alternatives (SC 1.1.1 — Level A)

#### Finding P-1: Decorative icons missing `aria-hidden`

- **Severity:** Minor
- **Files:** All step files, `MainLayout.razor`, `Confirmation.razor`, `Home.razor`, `StatusPage.razor`
- **Description:** MudBlazor `<MudIcon>` elements used as purely decorative (e.g., the RvHookup hero icon on Step 1, CloudUpload on Step 7, CheckCircle on Confirmation) are announced by screen readers as meaningless image elements. MudBlazor renders these as `<svg>` without `aria-hidden="true"` by default unless a title/aria-label is explicitly set.
- **WCAG Criterion:** 1.1.1 Non-text Content
- **Fix:** For every decorative `<MudIcon>`, add the HTML attribute `aria-hidden="true"`. For informational icons (e.g., error/warning severity icons inside `<MudAlert>`), MudBlazor's Alert component already provides accessible semantics, so no action is needed on those.
- **Affected components:**
  - `Step1_IntakeLanding.razor` — RvHookup hero icon (line ~50), Error hero icon (line ~24)
  - `Step7_AttachmentUploadStep.razor` — CloudUpload icon (line ~31), CameraAlt icon (line ~43), file-type icons in the attachment list
  - `Step8_ReviewSubmitStep.razor` — Save icon in attachment list (line ~137)
  - `Confirmation.razor` — CheckCircle hero icon (line ~8)
  - `Home.razor` — RvHookup hero icon
  - `StatusPage.razor` — Error, Schedule, Warning, EditNote hero icons
  - `Status.razor` — Search hero icon
  - `NotFound.razor` — HelpOutline hero icon
  - `Error.razor` — Error hero icon

#### Finding P-2: VIN camera-captured image lacks meaningful alt text when extraction fails

- **Severity:** Minor
- **Files:** `Step3_VinLookupStep.razor`
- **Description:** The `<img>` for the captured VIN photo has `alt="Captured VIN photo"` which is acceptable but could be improved. When VIN extraction succeeds, the alt text should include the extracted VIN (e.g., `"Photo of VIN plate — extracted: 1HGCM82633A004352"`).
- **WCAG Criterion:** 1.1.1 Non-text Content
- **Fix:** Dynamically set the `alt` attribute: when extraction succeeds, use `$"Photo of VIN plate — extracted: {extractedVin}"`, otherwise keep `"Captured VIN photo"`.

### 1.2 Adaptable (SC 1.3.1–1.3.6)

#### Finding P-3: Form fields lack programmatic label association

- **Severity:** Critical
- **Files:** `Step2_CustomerInfoStep.razor`, `Step3_VinLookupStep.razor`, `Step4_VehicleDetailsStep.razor`, `Step5_IssueDescriptionStep.razor`, `Status.razor`
- **Description:** Every form field uses a visual label pattern of `<MudText>` above a `<MudTextField>` or `<MudSelect>`, but no `Label` parameter is set on the MudBlazor input component. MudBlazor's `Label` parameter renders an associated `<label>` element and sets `aria-labelledby`. Without it, the `<input>` has no programmatic name — screen readers announce it as "edit text" or "combo box" with no field identification.
- **WCAG Criterion:** 1.3.1 Info and Relationships (Level A), 4.1.2 Name, Role, Value (Level A)
- **Fix:** Add `Label="..."` to every `<MudTextField>` and `<MudSelect>`. The visual `<MudText>` label above can remain for sighted users. Alternatively, the existing visual label can be converted to use `aria-labelledby` by adding an `id` to the `<MudText>` and referencing it. The `Label` approach is simpler and idiomatic for MudBlazor.
- **Affected fields (all missing `Label`):**
  - Step 2: First name, Last name, Email, Phone
  - Step 3: VIN
  - Step 4: Manufacturer, Model, Year
  - Step 5: Description, Issue Category, Urgency, RV Usage, Approx. Purchase Date
  - Status page: Confirmation Number

#### Finding P-4: Radio groups lack accessible group label (`fieldset`/`legend`)

- **Severity:** Major
- **Files:** `Step3_VinLookupStep.razor`, `Step5_IssueDescriptionStep.razor`
- **Description:** `<MudRadioGroup>` for "Previously seen RVs" (Step 3) and "Extended Warranty" (Step 5) lack a group label. Screen readers cannot determine the purpose of the radio group. MudBlazor does not render a `<fieldset>`/`<legend>` by default.
- **WCAG Criterion:** 1.3.1 Info and Relationships (Level A)
- **Fix:** Wrap each `<MudRadioGroup>` in a semantic `<fieldset>` with a `<legend>` containing the group label text, or use `aria-label` on the `<MudRadioGroup>` element (e.g., `aria-label="Previously seen RVs"` and `aria-label="Do you have an extended warranty?"`).

#### Finding P-5: Wizard step progress lacks semantic structure

- **Severity:** Major
- **Files:** `IntakeWizard.razor`
- **Description:** The step progress indicator `"Step 2 of 8"` is plain text inside `<MudText Typo="Typo.caption">`. There is no `<nav>` landmark, no ARIA `role="progressbar"` or `aria-valuemin`/`aria-valuemax`/`aria-valuenow`, and no live-region announcement when the step changes. Screen reader users have no way to orient themselves in the wizard flow.
- **WCAG Criterion:** 1.3.1 Info and Relationships, 4.1.3 Status Messages (Level AA, WCAG 2.1+)
- **Fix:** 
  1. Add `role="status"` and `aria-live="polite"` to the step counter element so step changes are announced.
  2. Consider adding a `role="progressbar"` element: `aria-valuemin="1"` `aria-valuemax="8"` `aria-valuenow="@_state.CurrentStep"` `aria-label="Wizard progress"`.
  3. Wrap the header bar in a `<nav aria-label="Wizard navigation">` landmark.

#### Finding P-6: Heading hierarchy skips levels

- **Severity:** Minor
- **Files:** Multiple
- **Description:** `MainLayout.razor` uses `Typo.h6` for the app bar title, but wizard steps use `Typo.h5` for step titles. The `<PageTitle>` generates a `<title>` not a heading. The page has no `<h1>`. `App.razor` uses `<FocusOnNavigate Selector="h1" />` but there is no `<h1>` element on any page. The heading hierarchy effectively starts at `<h5>` within the wizard steps.
- **WCAG Criterion:** 1.3.1 Info and Relationships (Level A)
- **Fix:** 
  1. Add a visually hidden `<h1>` at the top of the main content area (e.g., in `IntakeWizard.razor`): `<MudText Typo="Typo.h1" Class="visually-hidden">Service Request Intake</MudText>`.
  2. Change step titles from `Typo.h5` to `Typo.h2` since they are the primary section headings within the page.
  3. Change review section headings (Step 8) from `Typo.h6` to `Typo.h3`.
  4. Add a `sr-only` / `visually-hidden` CSS class: `position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0;`.

#### Finding P-7: `InputFile` elements have no accessible name

- **Severity:** Critical
- **Files:** `Step3_VinLookupStep.razor`, `Step7_AttachmentUploadStep.razor`
- **Description:** The `<InputFile>` elements used for camera capture (Step 3) and file upload (Step 7) are visually hidden using `opacity: 0.001` and positioned absolutely over their container. They have no `aria-label`, `title`, or associated `<label>`. Screen readers announce them as "Choose file" or just "button" with no context. Additionally, these are technically focusable but invisible, creating a confusing keyboard experience.
- **WCAG Criterion:** 1.3.1 Info and Relationships, 4.1.2 Name, Role, Value, 2.4.6 Headings and Labels
- **Fix:** Add `aria-label` to each `<InputFile>`:
  - Step 3 camera: `aria-label="Take a photo of your VIN"`
  - Step 7 file upload: `aria-label="Upload photos or videos of the issue"`
  - Step 7 camera: `aria-label="Take a photo of the issue"`

### 1.3 Distinguishable (SC 1.4.x)

#### Finding P-8: Color-only differentiation for AI "Suggested" chips

- **Severity:** Major
- **Files:** `Step5_IssueDescriptionStep.razor`
- **Description:** The "Suggested" chips next to Issue Category, Urgency, and RV Usage use `Color.Info` (blue) as the only visual indicator that the value was suggested by AI. Users who cannot perceive color differences cannot distinguish AI-suggested values from user-selected ones.
- **WCAG Criterion:** 1.4.1 Use of Color (Level A)
- **Fix:** The chips already display text "Suggested" which provides a non-color indicator. However, adding an icon (e.g., `@Icons.Material.Filled.AutoAwesome`) inside the chip would reinforce the distinction. Also add `aria-label="AI suggested value"` to make the purpose clear for screen readers.

#### Finding P-9: Focus indicator visibility

- **Severity:** Major
- **Files:** `app.css` (global)
- **Description:** No custom focus styles are defined. MudBlazor provides default focus styles but some are thin and low-contrast, particularly on the outlined inputs and toggle buttons in the wizard. The `h1:focus { outline: none; }` rule in `app.css` removes focus from headings entirely.
- **WCAG Criterion:** 1.4.11 Non-text Contrast (Level AA), 2.4.7 Focus Visible (Level AA), 2.4.11 Focus Not Obscured (Minimum) (WCAG 2.2, Level AA), 2.4.12 Focus Not Obscured (Enhanced) (WCAG 2.2, Level AAA)
- **Fix:**
  1. Remove `h1:focus { outline: none; }` from `app.css` or replace with a visible focus style.
  2. Add a global focus-visible style for interactive elements: `:focus-visible { outline: 2px solid var(--mud-palette-primary); outline-offset: 2px; }`.
  3. Ensure MudBlazor button focus rings meet the 3:1 contrast ratio against adjacent colors.

#### Finding P-10: Text spacing compliance

- **Severity:** Minor
- **Files:** `app.css`, `design-tokens.css`
- **Description:** No restrictions are placed on user text-spacing overrides, which is good. However, some elements use fixed `height` or `min-height` values that could clip text if users increase letter-spacing or line-height per SC 1.4.12. Specifically, the `.mud-button-root { min-height: 44px; }` and `.mud-input .mud-input-root { min-height: 44px; }` rules in `app.css` use fixed minimum heights.
- **WCAG Criterion:** 1.4.12 Text Spacing (Level AA)
- **Fix:** Change fixed `min-height` values to use `em` or `rem` units (e.g., `min-height: 2.75rem`) so they scale with user text-size preferences. Verify that no `overflow: hidden` rules clip text after spacing adjustments.

---

## 2. Operable (WCAG Principle 2)

### 2.1 Keyboard Accessible (SC 2.1.x)

#### Finding O-1: Diagnostic question toggle buttons not keyboard accessible

- **Severity:** Critical
- **Files:** `Step6_DiagnosticQuestionsStep.razor`
- **Description:** Diagnostic question options use `<MudButton>` elements styled as toggles. While MudButtons are natively keyboard-accessible (focusable, Enter/Space activatable), the selected/deselected state is conveyed only visually (filled vs. outlined variant). There is no `aria-pressed` attribute to communicate toggle state to screen readers. Additionally, the button group has no `role="group"` with an accessible label connecting the buttons to their question.
- **WCAG Criterion:** 2.1.1 Keyboard (Level A), 4.1.2 Name, Role, Value (Level A)
- **Fix:**
  1. Add `aria-pressed="@(isSelected ? "true" : "false")"` to each toggle `<MudButton>`.
  2. Wrap each question's option buttons in a `<div role="group" aria-label="@question.QuestionText">`.

#### Finding O-2: File drop zone not keyboard-operable

- **Severity:** Major
- **Files:** `Step7_AttachmentUploadStep.razor`
- **Description:** The file upload drop zones use `<MudPaper>` with an overlaid `<InputFile>`. While the `<InputFile>` is technically focusable, the visible text "Drag files here or click to browse" and the "Take a Photo" area suggest click/drag interaction only. There is no keyboard instruction or visual focus indicator on the drop zone paper element.
- **WCAG Criterion:** 2.1.1 Keyboard (Level A)
- **Fix:**
  1. Ensure the `<InputFile>` receives focus visually by adding `:focus-visible` styling to the parent `<MudPaper>` container (e.g., via a CSS rule on a wrapper class).
  2. Update the instructional text to include keyboard alternatives: "Drag files here, click to browse, or press Enter to open the file picker".

#### Finding O-3: Camera capture trigger relies on JS interop click simulation

- **Severity:** Minor
- **Files:** `Step3_VinLookupStep.razor`, `interop.js`
- **Description:** The VIN camera capture button uses `OnAdornmentClick` → `TriggerCameraCapture()` → `rvs_triggerClick()` JS interop to programmatically click the hidden `<InputFile>`. This works but the adornment icon button may not have an accessible name.
- **WCAG Criterion:** 2.1.1 Keyboard, 4.1.2 Name, Role, Value
- **Fix:** The MudTextField `AdornmentIcon` renders a clickable icon. Add `AdornmentAriaLabel="Take photo of VIN"` (MudBlazor v9 supports this) or use a separate `<MudIconButton>` with `aria-label="Take photo of VIN"` alongside the text field instead.

### 2.2 Enough Time (SC 2.2.x)

#### Finding O-4: No issues identified

- The wizard has no time limits. Transcription and VIN extraction API calls use progress spinners with no timeout that would lose user data. Session state is persisted to `sessionStorage`. **Compliant.**

### 2.3 Seizures (SC 2.3.x)

#### Finding O-5: Wizard step transition animation

- **Severity:** Minor
- **Files:** `app.css`
- **Description:** The `.wizard-step` class applies a 0.3s slide-up fade-in animation on each step change. While this is within safe thresholds, users with motion sensitivity (vestibular disorders) may find it disorienting.
- **WCAG Criterion:** 2.3.3 Animation from Interactions (WCAG 2.1 Level AAA, best practice for AA)
- **Fix:** Wrap the animation in a `prefers-reduced-motion` media query:
  ```css
  @media (prefers-reduced-motion: reduce) {
      .wizard-step {
          animation: none;
      }
  }
  ```

### 2.4 Navigable (SC 2.4.x)

#### Finding O-6: No skip-to-content link

- **Severity:** Major
- **Files:** `MainLayout.razor`
- **Description:** The layout includes `<MudAppBar>` as a fixed header. There is no "skip to main content" link that allows keyboard users to bypass the app bar navigation and jump directly to the page content.
- **WCAG Criterion:** 2.4.1 Bypass Blocks (Level A)
- **Fix:** Add a visually-hidden skip link as the first focusable element in `<MudLayout>`:
  ```html
  <a href="#main-content" class="skip-link">Skip to main content</a>
  ```
  Add `id="main-content"` to the `<MudMainContent>` element. Add CSS for `.skip-link` that is invisible until focused.

#### Finding O-7: Page focus management on step navigation

- **Severity:** Critical
- **Files:** `IntakeWizard.razor`
- **Description:** When the wizard advances to the next step, focus is not moved to the new step content. The DOM changes via Blazor rendering, but the browser focus remains wherever it was (typically the "Continue" button that was just clicked, which may no longer be in the DOM). Screen reader users are not informed that new content has loaded and must manually search for it.
- **WCAG Criterion:** 2.4.3 Focus Order (Level A)
- **Fix:** After each step transition, programmatically move focus to the step heading or the first focusable element of the new step. In Blazor, this requires:
  1. Add an `id` or `@ref` to each step's heading element.
  2. In `HandleNextStep()` and `HandlePreviousStep()`, after `StateHasChanged()`, call `await JS.InvokeVoidAsync("rvs_focusElement", elementRef)` or use `ElementReference.FocusAsync()`.
  3. Add `tabindex="-1"` to heading elements so they can receive programmatic focus without being in the tab order.

#### Finding O-8: Page titles not step-specific

- **Severity:** Minor
- **Files:** `IntakeWizard.razor`
- **Description:** The `<PageTitle>` is `"Service Request Intake — {LocationName}"` for all 8 wizard steps. Screen reader users navigating between browser tabs cannot distinguish which step they are on.
- **WCAG Criterion:** 2.4.2 Page Titled (Level A)
- **Fix:** Update the `<PageTitle>` to include the current step: `"Step @_state.CurrentStep: @StepName — @_state.Config?.LocationName"` where `StepName` maps step numbers to names (Contact, VIN Lookup, Vehicle Details, etc.).

#### Finding O-9: Landmark regions incomplete

- **Severity:** Major
- **Files:** `MainLayout.razor`
- **Description:** `<MudAppBar>` renders as a `<header>` element which is good. `<MudMainContent>` renders as a `<main>` element. However, there is no `<nav>` landmark for the wizard navigation header (Back button + step counter). The wizard step content has no `role="region"` or `aria-label` to distinguish it from other page content.
- **WCAG Criterion:** 2.4.1 Bypass Blocks, 1.3.1 Info and Relationships
- **Fix:**
  1. Wrap the wizard header bar (Back button + step counter) in `<nav aria-label="Wizard navigation">`.
  2. Add `role="region" aria-label="Step content"` to the wizard step `<div>` container.

### 2.5 Input Modalities (SC 2.5.x — WCAG 2.1 / 2.2)

#### Finding O-10: Target size for close/remove buttons

- **Severity:** Major
- **Files:** `Step3_VinLookupStep.razor`, `Step7_AttachmentUploadStep.razor`
- **Description:** The "Remove captured photo" (Step 3) and "Remove file" (Step 7) icon buttons use `Size="Size.Small"` which renders at approximately 28×28px. WCAG 2.2 SC 2.5.8 Target Size (Minimum) requires a minimum of 24×24px with adequate spacing, but SC 2.5.5 Target Size (Enhanced, AAA) recommends 44×44px. The `app.css` rule `.mud-button-root { min-height: 44px; }` only targets `MudButton`, not `MudIconButton`.
- **WCAG Criterion:** 2.5.8 Target Size (Minimum) (WCAG 2.2, Level AA)
- **Fix:** Either change `Size` to `Size.Medium` for these icon buttons, or add a CSS rule: `.mud-icon-button { min-width: 44px; min-height: 44px; }` to ensure all icon buttons meet the 44px minimum tap target.

#### Finding O-11: Dragging alternatives for file upload

- **Severity:** Minor
- **Files:** `Step7_AttachmentUploadStep.razor`
- **Description:** WCAG 2.2 SC 2.5.7 Dragging Movements requires that any functionality that uses dragging can be achieved with a single pointer. The file upload area supports drag-and-drop but also has a click-to-browse fallback via the hidden `<InputFile>`, so this is **already compliant**. The camera button provides a separate single-pointer method. **No fix needed.**

---

## 3. Understandable (WCAG Principle 3)

### 3.1 Readable (SC 3.1.x)

#### Finding U-1: Language attribute is set correctly

- **Files:** `index.html`
- **Description:** `<html lang="en">` is present. **Compliant.**

### 3.2 Predictable (SC 3.2.x)

#### Finding U-2: Auto-advance after VIN extraction at high confidence

- **Severity:** Minor
- **Files:** `Step3_VinLookupStep.razor`
- **Description:** When VIN extraction from a photo succeeds with confidence ≥ 0.9, the step auto-triggers a VIN lookup and advances to the next step without explicit user action. This could be disorienting for screen reader users who don't receive an announcement before the page changes.
- **WCAG Criterion:** 3.2.2 On Input (Level A)
- **Fix:** Before auto-advancing, briefly show a status message (e.g., "VIN found — proceeding to vehicle details…") using an `aria-live="assertive"` region. Add a ~1.5 second delay before auto-advancing to give users time to perceive the change. Alternatively, remove the auto-advance and require an explicit "Continue" click even at high confidence.

### 3.3 Input Assistance (SC 3.3.x)

#### Finding U-3: Validation errors not programmatically associated

- **Severity:** Critical
- **Files:** `Step2_CustomerInfoStep.razor`, `Step3_VinLookupStep.razor`, `Step4_VehicleDetailsStep.razor`, `Step5_IssueDescriptionStep.razor`
- **Description:** MudBlazor's `Validation` function-based approach renders error text below the field, but without `Label` set on the `<MudTextField>`, the error text is not programmatically linked to the input via `aria-describedby`. Screen readers do not announce validation errors when the user tabs into or interacts with the field.
- **WCAG Criterion:** 3.3.1 Error Identification (Level A), 3.3.2 Labels or Instructions (Level A)
- **Fix:** Setting `Label="..."` on each field (see Finding P-3) will cause MudBlazor to generate proper `aria-describedby` associations between the input and its error/helper text. This single fix resolves both P-3 and U-3.

#### Finding U-4: Error summary not provided on validation failure

- **Severity:** Major
- **Files:** `IntakeWizard.razor`, all step components
- **Description:** When a user clicks "Continue" and validation fails, individual field errors appear below each field. However, there is no error summary at the top of the form, and focus is not moved to the first error. Users may not realize that validation failed, especially if the error is off-screen.
- **WCAG Criterion:** 3.3.1 Error Identification (Level A)
- **Fix:**
  1. Add an error summary `<MudAlert>` at the top of each step form that appears when `_submitted && !_form.IsValid`. The alert should use `role="alert"` or `Severity.Error` (MudBlazor alerts include `role="alert"` by default for Error severity).
  2. After validation failure, programmatically focus the error summary or the first invalid field.

#### Finding U-5: Submit error announcement

- **Severity:** Minor
- **Files:** `Step8_ReviewSubmitStep.razor`
- **Description:** The submit error alert already uses `role="alert"` which is good. However, the upload progress status text does not use a live region — screen readers will not announce upload progress changes.
- **WCAG Criterion:** 4.1.3 Status Messages (Level AA)
- **Fix:** Add `aria-live="polite"` to the upload progress status text container.

---

## 4. Robust (WCAG Principle 4)

### 4.1 Compatible (SC 4.1.x)

#### Finding R-1: MudBlazor component ARIA roles

- **Severity:** Minor
- **Files:** All
- **Description:** MudBlazor generally provides correct ARIA roles for its components (`role="textbox"`, `role="combobox"`, `role="radio"`, etc.). The `<MudCheckBox>` in Step 2 (notification preferences) renders with proper checkbox semantics. `<MudSelect>` uses `role="combobox"` with `aria-expanded`. **Mostly compliant** — no systemic issues found.

#### Finding R-2: Live region for loading/busy states

- **Severity:** Major
- **Files:** `IntakeWizard.razor`, `Step1_IntakeLanding.razor`, `Step6_DiagnosticQuestionsStep.razor`, `StatusPage.razor`
- **Description:** Loading states use `<MudProgressCircular>` with visual spinners. Step 1 and Step 6 include `aria-busy="true"` and `aria-label` on the loading container, which is good. However, the main wizard loading state in `IntakeWizard.razor` (line ~8) and `StatusPage.razor` (line ~4) lack `aria-busy="true"` and `aria-label` on their loading containers.
- **WCAG Criterion:** 4.1.3 Status Messages (Level AA)
- **Fix:** Add `aria-busy="true" aria-label="Loading"` to the loading containers in `IntakeWizard.razor` and `StatusPage.razor`, matching the pattern already used in `Step1_IntakeLanding.razor` and `Step6_DiagnosticQuestionsStep.razor`.

#### Finding R-3: Dynamic content announcement for AI operations

- **Severity:** Major
- **Files:** `Step5_IssueDescriptionStep.razor`
- **Description:** When speech-to-text transcription completes, AI suggestions populate the category/urgency/usage fields, and "Suggested" chips appear — none of these changes are announced to screen readers. The transcription status ("Transcribing...", "Cleaning up transcript...") uses visual spinners but no live-region announcements.
- **WCAG Criterion:** 4.1.3 Status Messages (Level AA)
- **Fix:**
  1. Wrap the transcription/refinement status area in an `aria-live="polite"` container.
  2. When an AI suggestion populates a field, add a visually-hidden `aria-live="assertive"` announcement: e.g., "AI suggested category: Electrical".
  3. When transcription completes, announce "Transcription complete" via an `aria-live` region.

---

## 5. WCAG 2.2 New Success Criteria

### 5.1 Focus Not Obscured (SC 2.4.11 — Level AA, new in 2.2)

#### Finding W-1: MudAppBar could obscure focus

- **Severity:** Minor
- **Files:** `MainLayout.razor`, `app.css`
- **Description:** The `<MudAppBar>` is a fixed/sticky header (64px tall, offset by `.mud-main-content { padding-top: 64px !important; }`). When the user tabs through elements at the top of the wizard step content, focused elements may be partially obscured behind the app bar if the browser scroll-to-focus behavior doesn't account for the fixed header.
- **WCAG Criterion:** 2.4.11 Focus Not Obscured (Minimum) (WCAG 2.2, Level AA)
- **Fix:** Add `scroll-padding-top: 80px;` to the `html` or `.mud-main-content` element in `app.css` to ensure focused elements are scrolled into view below the app bar. Also add `scroll-margin-top: 80px;` to focusable elements in the wizard step area.

### 5.2 Focus Not Obscured (SC 2.4.12 — Level AAA, new in 2.2)

- **Description:** Level AAA requires no part of the focused element is obscured. The `scroll-padding-top` fix from W-1 should achieve this as well.

### 5.3 Dragging Movements (SC 2.5.7 — Level AA, new in 2.2)

- **Description:** Already addressed in Finding O-11. File upload has click-to-browse alternative. **Compliant.**

### 5.4 Target Size (SC 2.5.8 — Level AA, new in 2.2)

- **Description:** Already addressed in Finding O-10. Small icon buttons need 44px minimum.

### 5.5 Consistent Help (SC 3.2.6 — Level A, new in 2.2)

#### Finding W-2: No consistent help mechanism

- **Severity:** Minor
- **Files:** All
- **Description:** The intake wizard provides contextual help text (sub-labels under field names, help text on diagnostic questions, alerts about pre-fill and VIN lookup status). However, there is no consistent help mechanism (e.g., a help link or support contact) available on every page.
- **WCAG Criterion:** 3.2.6 Consistent Help (WCAG 2.2, Level A)
- **Fix:** Add a persistent "Need help?" link or contact info in the app bar or footer that is visible on every page. This could link to a dealer phone number or support email configured per location.

### 5.6 Redundant Entry (SC 3.3.7 — Level A, new in 2.2)

- **Description:** The wizard persists all data to `sessionStorage` and pre-fills fields when navigating back. The review step allows editing individual sections and returns to review after. VIN lookup auto-populates vehicle details. **Compliant.**

### 5.7 Accessible Authentication (SC 3.3.8 — Level AA, new in 2.2)

- **Description:** The intake wizard is unauthenticated (public-facing). The confirmation number lookup on the Status page does not require a cognitive function test — it is a copy-paste token from email. **Compliant.**

---

## Implementation Priority

### Phase 1 — Critical (must fix for basic AT operability)

| # | Finding | Fix Summary | Files |
|---|---|---|---|
| 1 | P-3 | Add `Label` to all form fields | Steps 2, 3, 4, 5; Status.razor |
| 2 | P-7 | Add `aria-label` to all `<InputFile>` elements | Steps 3, 7 |
| 3 | O-1 | Add `aria-pressed` to diagnostic toggle buttons | Step 6 |
| 4 | O-7 | Focus management on step transitions | IntakeWizard.razor, interop.js |
| 5 | U-3 | (Resolved by P-3) Validation error association | Steps 2, 3, 4, 5 |
| 6 | U-4 | Add error summary on validation failure | All step components |

### Phase 2 — Major (significantly improves AT experience)

| # | Finding | Fix Summary | Files |
|---|---|---|---|
| 7 | P-4 | Add group labels to radio groups | Steps 3, 5 |
| 8 | P-5 | Add ARIA to wizard step progress | IntakeWizard.razor |
| 9 | P-8 | Reinforce AI suggestion chips with icon | Step 5 |
| 10 | P-9 | Add visible focus indicators | app.css |
| 11 | O-2 | Keyboard instructions on file drop zone | Step 7 |
| 12 | O-6 | Add skip-to-content link | MainLayout.razor, app.css |
| 13 | O-9 | Complete landmark regions | MainLayout.razor, IntakeWizard.razor |
| 14 | O-10 | Increase icon button target size | app.css |
| 15 | R-2 | Add aria-busy to all loading states | IntakeWizard.razor, StatusPage.razor |
| 16 | R-3 | Live region for AI status announcements | Step 5 |
| 17 | U-4 | Validation error focus management | All step components |

### Phase 3 — Minor (polish and best practices)

| # | Finding | Fix Summary | Files |
|---|---|---|---|
| 18 | P-1 | Add `aria-hidden` to decorative icons | All pages |
| 19 | P-2 | Dynamic alt text for VIN photo | Step 3 |
| 20 | P-6 | Fix heading hierarchy | All pages, app.css |
| 21 | P-10 | Use relative units for tap targets | app.css |
| 22 | O-3 | Camera adornment accessible name | Step 3 |
| 23 | O-5 | Respect prefers-reduced-motion | app.css |
| 24 | O-8 | Step-specific page titles | IntakeWizard.razor |
| 25 | U-2 | Announce before VIN auto-advance | Step 3 |
| 26 | U-5 | Upload progress live region | Step 8 |
| 27 | W-1 | Scroll padding for fixed header | app.css |
| 28 | W-2 | Consistent help mechanism | MainLayout.razor |

---

## Testing Strategy

### Automated

- Run **axe-core** (via `Deque.AxeAccessibility` or browser extension) against the running Blazor WASM app to catch automatically detectable issues (estimated 40–60% of findings).
- Add **Playwright** accessibility snapshot tests if integration tests exist for the intake wizard.

### Manual

- **Keyboard-only testing:** Complete the full 8-step wizard flow using only Tab, Shift+Tab, Enter, Space, and Arrow keys. Verify every interactive element is reachable and operable.
- **Screen reader testing:** Complete the wizard with NVDA (Windows) and VoiceOver (macOS/iOS). Verify:
  - Step names are announced on navigation.
  - Form fields are identified correctly.
  - Validation errors are announced.
  - AI suggestions are announced.
  - Submit success/failure is announced.
- **Zoom testing:** Complete the wizard at 200% and 400% browser zoom. Verify no content is clipped or overlapping.
- **High-contrast mode:** Verify the existing ThemeSwitcher high-contrast theme meets 4.5:1 (normal text) and 3:1 (large text, UI components) contrast ratios.
- **Mobile assistive technology:** Test with TalkBack (Android) and VoiceOver (iOS) on actual devices.

---

## Estimated Effort

| Phase | Est. Hours | Notes |
|---|---|---|
| Phase 1 (Critical) | 6–8 | Mostly adding attributes; focus management requires JS interop |
| Phase 2 (Major) | 8–10 | CSS changes, landmark restructuring, live regions |
| Phase 3 (Minor) | 4–6 | Polish, heading hierarchy, reduced-motion |
| Testing & validation | 4–6 | Manual AT testing, axe-core, fixes from testing |
| **Total** | **22–30** | |
