using System.Text.Json;
using Microsoft.JSInterop;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Validation;

namespace RVS.Blazor.Intake.State;

/// <summary>
/// Shared state container for the 8-step intake wizard.
/// Holds all step data, current step index, validation state, and returning customer prefill data.
/// Persisted in <c>sessionStorage</c> for offline resilience.
/// </summary>
public sealed class IntakeWizardState
{
    private const string StorageKey = "rvs_intake_wizard_state";
    private const int TotalStepCount = 8;
    private const int MaxDescriptionLength = 2000;
    private const int MaxAttachments = 10;

    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeWizardState"/> with the JS runtime
    /// for sessionStorage persistence. In standalone WASM, JS interop is available immediately.
    /// </summary>
    public IntakeWizardState(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _jsRuntime = jsRuntime;
    }

    /// <summary>Current wizard step index (1-based, range 1–8).</summary>
    public int CurrentStep { get; private set; } = 1;

    /// <summary>Total number of wizard steps.</summary>
    public int TotalSteps => TotalStepCount;

    /// <summary>Location slug from URL route.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional magic-link token from the intake URL query string.</summary>
    public string? Token { get; set; }

    /// <summary>Location configuration fetched from the API.</summary>
    public IntakeConfigResponseDto? Config { get; set; }

    /// <summary>Customer contact information (Step 2).</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Customer last name (Step 2).</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Customer email address — required (Step 2).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Customer phone number — optional (Step 2).</summary>
    public string? Phone { get; set; }

    /// <summary>Whether the customer info was prefilled from a magic-link token.</summary>
    public bool IsPrefilled { get; set; }

    /// <summary>All known vehicles for the returning customer, enabling one-tap VIN selection in Step 3.</summary>
    public List<AssetInfoDto> KnownAssets { get; set; } = [];

    /// <summary>Vehicle Identification Number (Step 3).</summary>
    public string Vin { get; set; } = string.Empty;

    /// <summary>Vehicle manufacturer (Step 4).</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Vehicle model (Step 4).</summary>
    public string? Model { get; set; }

    /// <summary>Vehicle year (Step 4).</summary>
    public int? Year { get; set; }

    /// <summary>Issue category selected from LookupSet (Step 5).</summary>
    public string IssueCategory { get; set; } = string.Empty;

    /// <summary>Issue description text, max 2000 characters (Step 5).</summary>
    public string IssueDescription { get; set; } = string.Empty;

    /// <summary>Urgency level (Step 5).</summary>
    public string? Urgency { get; set; }

    /// <summary>RV usage type — e.g., "Full-Time" or "Part-Time" (Step 5).</summary>
    public string? RvUsage { get; set; }

    /// <summary>AI-generated diagnostic questions (Step 6).</summary>
    public List<DiagnosticQuestionDto> DiagnosticQuestions { get; set; } = [];

    /// <summary>Customer's diagnostic responses (Step 6).</summary>
    public List<DiagnosticResponseDto> DiagnosticResponses { get; set; } = [];

    /// <summary>Smart suggestion from AI diagnostic (Step 6).</summary>
    public string? SmartSuggestion { get; set; }

    /// <summary>Uploaded attachment metadata (Step 7).</summary>
    public List<AttachmentFileInfo> Attachments { get; set; } = [];

    /// <summary>Whether the service request has been submitted.</summary>
    public bool IsSubmitted { get; set; }

    /// <summary>The created service request ID after submission.</summary>
    public string? CreatedServiceRequestId { get; set; }

    /// <summary>
    /// When set, the next call to <see cref="GoToNextStepAsync"/> or <see cref="GoToPreviousStepAsync"/>
    /// will navigate to this step instead of the natural next/previous step.
    /// Used when editing a specific section from the Review &amp; Submit page.
    /// </summary>
    public int? ReturnToStepAfterEdit { get; set; }

    /// <summary>Event raised when state changes to notify UI components.</summary>
    public event Action? OnChange;

    /// <summary>
    /// Navigates to the next wizard step if validation passes.
    /// If <see cref="ReturnToStepAfterEdit"/> is set, navigates there instead and clears the flag.
    /// </summary>
    public async Task GoToNextStepAsync()
    {
        if (ReturnToStepAfterEdit is { } returnStep)
        {
            ReturnToStepAfterEdit = null;
            CurrentStep = returnStep;
            NotifyStateChanged();
            await PersistAsync();
            return;
        }

        if (CurrentStep < TotalStepCount)
        {
            CurrentStep++;
            NotifyStateChanged();
            await PersistAsync();
        }
    }

    /// <summary>
    /// Navigates to the previous wizard step.
    /// If <see cref="ReturnToStepAfterEdit"/> is set, navigates there instead and clears the flag.
    /// </summary>
    public async Task GoToPreviousStepAsync()
    {
        if (ReturnToStepAfterEdit is { } returnStep)
        {
            ReturnToStepAfterEdit = null;
            CurrentStep = returnStep;
            NotifyStateChanged();
            await PersistAsync();
            return;
        }

        if (CurrentStep > 1)
        {
            CurrentStep--;
            NotifyStateChanged();
            await PersistAsync();
        }
    }

    /// <summary>
    /// Navigates to a specific wizard step for editing.
    /// </summary>
    public async Task GoToStepAsync(int step)
    {
        if (step >= 1 && step <= TotalStepCount)
        {
            CurrentStep = step;
            NotifyStateChanged();
            await PersistAsync();
        }
    }

    /// <summary>
    /// Applies customer prefill data from the intake config (magic-link token).
    /// </summary>
    public void ApplyPrefill(CustomerInfoDto prefill)
    {
        ArgumentNullException.ThrowIfNull(prefill);
        FirstName = prefill.FirstName;
        LastName = prefill.LastName;
        Email = prefill.Email;
        Phone = prefill.Phone;
        IsPrefilled = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Applies asset prefill data from the intake config (magic-link token).
    /// Sets the most recently used vehicle information so the customer doesn't re-enter it.
    /// </summary>
    public void ApplyAssetPrefill(AssetInfoDto prefillAsset)
    {
        ArgumentNullException.ThrowIfNull(prefillAsset);
        Vin = prefillAsset.AssetId;
        Manufacturer = prefillAsset.Manufacturer;
        Model = prefillAsset.Model;
        Year = prefillAsset.Year;
        NotifyStateChanged();
    }

    /// <summary>
    /// Validates the current step and returns any error messages.
    /// </summary>
    public List<string> ValidateCurrentStep()
    {
        return CurrentStep switch
        {
            1 => ValidateLanding(),
            2 => ValidateCustomerInfo(),
            3 => ValidateVinLookup(),
            4 => [],
            5 => ValidateIssueDescription(),
            6 => ValidateDiagnosticQuestions(),
            7 => ValidateAttachments(),
            8 => [],
            _ => []
        };
    }

    /// <summary>
    /// Builds the <see cref="ServiceRequestCreateRequestDto"/> from the current wizard state.
    /// </summary>
    public ServiceRequestCreateRequestDto BuildCreateRequest()
    {
        return new ServiceRequestCreateRequestDto
        {
            Customer = new CustomerInfoDto
            {
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                Email = Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim()
            },
            Asset = new AssetInfoDto
            {
                AssetId = Vin.Trim().ToUpperInvariant(),
                Manufacturer = string.IsNullOrWhiteSpace(Manufacturer) ? null : Manufacturer.Trim(),
                Model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim(),
                Year = Year
            },
            IssueCategory = IssueCategory.Trim(),
            IssueDescription = IssueDescription.Trim(),
            Urgency = string.IsNullOrWhiteSpace(Urgency) ? null : Urgency.Trim(),
            RvUsage = string.IsNullOrWhiteSpace(RvUsage) ? null : RvUsage.Trim(),
            DiagnosticResponses = DiagnosticResponses.Count > 0 ? DiagnosticResponses : null
        };
    }

    /// <summary>
    /// Persists the current state to sessionStorage.
    /// </summary>
    public async Task PersistAsync()
    {
        var data = new IntakeWizardStateData
        {
            CurrentStep = CurrentStep,
            Slug = Slug,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            Phone = Phone,
            IsPrefilled = IsPrefilled,
            KnownAssets = KnownAssets,
            Vin = Vin,
            Manufacturer = Manufacturer,
            Model = Model,
            Year = Year,
            IssueCategory = IssueCategory,
            IssueDescription = IssueDescription,
            Urgency = Urgency,
            RvUsage = RvUsage,
            DiagnosticResponses = DiagnosticResponses,
            SmartSuggestion = SmartSuggestion,
            IsSubmitted = IsSubmitted,
            CreatedServiceRequestId = CreatedServiceRequestId
        };

        var json = JsonSerializer.Serialize(data);
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
    }

    /// <summary>
    /// Restores state from sessionStorage if available.
    /// </summary>
    public async Task RestoreAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(json)) return;

            var data = JsonSerializer.Deserialize<IntakeWizardStateData>(json);
            if (data is null) return;

            CurrentStep = data.CurrentStep;
            Slug = data.Slug;
            FirstName = data.FirstName;
            LastName = data.LastName;
            Email = data.Email;
            Phone = data.Phone;
            IsPrefilled = data.IsPrefilled;
            KnownAssets = data.KnownAssets;
            Vin = data.Vin;
            Manufacturer = data.Manufacturer;
            Model = data.Model;
            Year = data.Year;
            IssueCategory = data.IssueCategory;
            IssueDescription = data.IssueDescription;
            Urgency = data.Urgency;
            RvUsage = data.RvUsage;
            DiagnosticResponses = data.DiagnosticResponses;
            SmartSuggestion = data.SmartSuggestion;
            IsSubmitted = data.IsSubmitted;
            CreatedServiceRequestId = data.CreatedServiceRequestId;

            NotifyStateChanged();
        }
        catch (JsonException)
        {
            // Corrupted data — start fresh
            await ClearAsync();
        }
    }

    /// <summary>
    /// Clears all state and removes from sessionStorage.
    /// </summary>
    public async Task ClearAsync()
    {
        CurrentStep = 1;
        Slug = string.Empty;
        Config = null;
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = string.Empty;
        Phone = null;
        IsPrefilled = false;
        KnownAssets = [];
        Vin = string.Empty;
        Manufacturer = null;
        Model = null;
        Year = null;
        IssueCategory = string.Empty;
        IssueDescription = string.Empty;
        Urgency = null;
        RvUsage = null;
        DiagnosticQuestions = [];
        DiagnosticResponses = [];
        SmartSuggestion = null;
        Attachments = [];
        IsSubmitted = false;
        CreatedServiceRequestId = null;

        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);

        NotifyStateChanged();
    }

    /// <summary>
    /// Builds the URL to navigate to when starting over, preserving the location slug and optional token.
    /// </summary>
    public static string BuildStartOverUrl(string slug, string? token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var url = $"/intake/{Uri.EscapeDataString(slug)}";
        if (!string.IsNullOrWhiteSpace(token))
        {
            url += $"?token={Uri.EscapeDataString(token)}";
        }

        return url;
    }

    /// <summary>
    /// Notifies subscribers and persists state.
    /// </summary>
    public async Task NotifyAndPersistAsync()
    {
        NotifyStateChanged();
        await PersistAsync();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    private List<string> ValidateLanding()
    {
        var errors = new List<string>();
        if (Config is null)
        {
            errors.Add("Location configuration has not been loaded.");
        }

        return errors;
    }

    private List<string> ValidateCustomerInfo()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(FirstName))
            errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(LastName))
            errors.Add("Last name is required.");
        if (string.IsNullOrWhiteSpace(Email))
            errors.Add("Email is required.");
        else
        {
            var emailResult = EmailValidator.Validate(Email);
            if (!emailResult.IsValid)
                errors.Add(emailResult.ErrorMessage!);
        }

        return errors;
    }

    private List<string> ValidateVinLookup()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Vin))
        {
            errors.Add("VIN is required.");
        }
        else
        {
            var vinResult = ClientVinValidator.ValidateFormat(Vin);
            if (!vinResult.IsValid)
                errors.Add(vinResult.ErrorMessage!);
        }

        return errors;
    }

    private List<string> ValidateIssueDescription()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(IssueCategory))
            errors.Add("Issue category is required.");
        if (string.IsNullOrWhiteSpace(IssueDescription))
            errors.Add("Issue description is required.");
        else if (IssueDescription.Length > MaxDescriptionLength)
            errors.Add($"Issue description must not exceed {MaxDescriptionLength} characters.");

        return errors;
    }

    private static List<string> ValidateDiagnosticQuestions()
    {
        // Diagnostic responses are optional — AI step can be skipped
        return [];
    }

    private List<string> ValidateAttachments()
    {
        var errors = new List<string>();
        if (Attachments.Count > MaxAttachments)
            errors.Add($"Maximum {MaxAttachments} attachments allowed.");

        return errors;
    }
}

/// <summary>
/// Metadata for a file selected/uploaded in the attachment step.
/// Holds a reference to the <see cref="Microsoft.AspNetCore.Components.Forms.IBrowserFile"/>
/// so the file stream can be read at upload time.
/// </summary>
public sealed class AttachmentFileInfo
{
    /// <summary>Original file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Whether the file has been uploaded to blob storage.</summary>
    public bool IsUploaded { get; set; }

    /// <summary>Upload progress percentage (0–100).</summary>
    public int UploadProgressPercent { get; set; }

    /// <summary>Error message if upload failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The blob name returned after upload for the confirm step.</summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Reference to the browser file for streaming at upload time.
    /// Not serializable — only available during the current browser session.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.AspNetCore.Components.Forms.IBrowserFile? BrowserFile { get; set; }
}

/// <summary>
/// Serializable data transfer object for sessionStorage persistence.
/// Excludes non-serializable properties like Config and DiagnosticQuestions.
/// </summary>
internal sealed class IntakeWizardStateData
{
    public int CurrentStep { get; set; } = 1;
    public string Slug { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsPrefilled { get; set; }
    public List<AssetInfoDto> KnownAssets { get; set; } = [];
    public string Vin { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string IssueCategory { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public string? Urgency { get; set; }
    public string? RvUsage { get; set; }
    public List<DiagnosticResponseDto> DiagnosticResponses { get; set; } = [];
    public string? SmartSuggestion { get; set; }
    public bool IsSubmitted { get; set; }
    public string? CreatedServiceRequestId { get; set; }
}
