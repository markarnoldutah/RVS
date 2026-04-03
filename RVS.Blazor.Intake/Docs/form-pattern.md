# Form Page Pattern — MudBlazor Intake Style

Reference document for wizard step pages in `RVS.Blazor.Intake`.
Derive all new step pages from this pattern. Do not deviate without a documented reason.

---

## 1. Page Structure

Each step is a standalone Blazor component located in `Pages/Steps/`.

```
Pages/
  Steps/
    StepN_DescriptiveNameStep.razor
    StepN_DescriptiveNameStep.razor.css   ← always present; scoped CSS for borders
```

The component receives two parameters from the wizard host:

```razor
[Parameter] public IntakeWizardState State { get; set; } = default!;
[Parameter] public EventCallback OnNext { get; set; }
```

---

## 2. Card Container

```razor
<MudPaper Elevation="2" Class="pa-6 rounded-lg">
    <!-- step content -->
</MudPaper>
```

- `Elevation="2"` — standard card depth for step pages
- `pa-6` — inner padding; reduce to `pa-4` only on very compact steps
- `rounded-lg` — consistent rounded corners across all steps

---

## 3. Page Header

```razor
<MudText Typo="Typo.h5" Class="mb-1">Page Title</MudText>
<MudText Typo="Typo.body1" Style="color: var(--mud-palette-text-secondary);" Class="mb-4">
    Supporting description sentence.
</MudText>
```

- `Typo.h5` for the page title — never raw `<h2>` or `<h3>`
- Description uses `var(--mud-palette-text-secondary)` — NOT `Color.Secondary`
- `mb-4` separates header from first field group

---

## 4. Field Group Pattern

```razor
<MudStack Spacing="1" Class="mb-4">
    <MudText Typo="Typo.body1" Style="font-weight: 700;">Field Label</MudText>
    <MudText Typo="Typo.body1" Style="color: var(--mud-palette-text-secondary);">
        Helper or descriptive text for this field.
    </MudText>
    <MudTextField
        @bind-Value="State.PropertyName"
        Variant="Variant.Outlined"
        Margin="Margin.Dense"
        Placeholder="e.g. placeholder value" />
</MudStack>
```

### Label Rules

| Property | Value |
|---|---|
| Typo | `Typo.body1` |
| font-weight | `700` (bold via inline style) |
| Color | Default — inherits theme text color |

Do **not** use `Typo.h6` — too heavy for field labels.  
Do **not** use `Color.Secondary` — that is brand teal `#00897B`, not neutral gray.

### Helper Text Rules

| Property | Value |
|---|---|
| Typo | `Typo.body1` |
| Style | `color: var(--mud-palette-text-secondary);` |

Always use the CSS custom property — never `Color.Secondary` or a hardcoded hex.

### Input Field Rules

| Parameter | Value |
|---|---|
| Variant | `Variant.Outlined` |
| Margin | `Margin.Dense` |
| Label | *(omit — label is the `MudText` above)* |
| Placeholder | Brief example value (optional) |
| HelperText | *(omit — use the `MudText` description instead)* |

Do **not** use the MudTextField `Label` parameter. The visual label is the `MudText` element above the field.

### Multi-Column Fields

```razor
<MudGrid Spacing="3">
    <MudItem xs="12" sm="6">
        <MudStack Spacing="1"><!-- field group A --></MudStack>
    </MudItem>
    <MudItem xs="12" sm="6">
        <MudStack Spacing="1"><!-- field group B --></MudStack>
    </MudItem>
</MudGrid>
```

Stack vertically on `xs`; side-by-side on `sm` and above.

---

## 5. Scoped CSS — Border Weight

Every step page **must** have a companion `.razor.css` file:

```css
/* StepN_DescriptiveNameStep.razor.css */

::deep .mud-input-outlined-border {
    border-width: 2px !important;
}
```

---

## 6. Primary Action Button

```razor
<MudButton
    Variant="Variant.Filled"
    Color="Color.Primary"
    FullWidth="true"
    Class="mt-6"
    OnClick="HandleNext">
    Continue
</MudButton>
```

- `Variant.Filled` + `Color.Primary` — never `Variant.Outlined` for the primary CTA
- `FullWidth="true"` on mobile-first wizard steps
- Add `Disabled="_isSaving"` during async operations

---

## 7. Step Indicator

```razor
<MudText
    Typo="Typo.caption"
    Align="Align.Center"
    Style="color: var(--mud-palette-text-secondary);"
    Class="mt-2">
    Step 2 of 7
</MudText>
```

---

## 8. Code-Behind Pattern

```csharp
@code {
    [Parameter] public IntakeWizardState State { get; set; } = default!;
    [Parameter] public EventCallback OnNext { get; set; }

    private async Task HandleNext()
    {
        await State.NotifyAndPersistAsync();
        await OnNext.InvokeAsync();
    }
}
```

Keep `@code` blocks minimal. Push business logic into services or `IntakeWizardState`.

---

## 9. Color Token Rules

| Use Case | Correct | Wrong |
|---|---|---|
| Muted / helper / caption text | `var(--mud-palette-text-secondary)` | `Color.Secondary` |
| Brand accent UI chrome | `Color.Secondary` | `var(--mud-palette-text-secondary)` |
| Primary action button | `Color.Primary` | `Color.Secondary` |
| Error messages | `Severity.Error` on `MudAlert` | Hardcoded hex |

**`Color.Secondary` = brand teal `#00897B`. It is a UI chrome color — never use it for text coloring.**

---

## 10. Theme Awareness Checklist

| Check | Light | Dark | High Contrast |
|---|---|---|---|
| All text visible | ✓ | ✓ | ✓ |
| Input borders visible | ✓ | ✓ | ✓ |
| Helper text readable | ✓ | ✓ | ✓ |
| Button text readable on Primary background | ✓ | ✓ | ✓ (black text) |
| No hardcoded color values in Style= | ✓ | ✓ | ✓ |

Trigger themes via the three-way toggle in the app bar (`ThemeSwitcher.razor`).

---

## 11. New Step File Checklist

- [ ] `Pages/Steps/StepN_DescriptiveNameStep.razor` created
- [ ] `Pages/Steps/StepN_DescriptiveNameStep.razor.css` created with `::deep .mud-input-outlined-border` rule
- [ ] Parameters: `IntakeWizardState State` and `EventCallback OnNext`
- [ ] All labels: `Typo.body1` + `font-weight: 700`
- [ ] All helper text: `Typo.body1` + `color: var(--mud-palette-text-secondary)`
- [ ] All inputs: `Variant.Outlined` + `Margin.Dense` + no `Label` parameter
- [ ] Primary button: `Variant.Filled` + `Color.Primary` + `FullWidth="true"`
- [ ] No `Color.Secondary` used on any text element
- [ ] No hardcoded hex colors in `Style=` attributes
- [ ] Tested in Light, Dark, and High Contrast modes

---

## 12. Reference Implementation

`Pages/Steps/Step2_CustomerInfoStep.razor` is the canonical reference for this pattern.
Consult it first before building a new step page.