namespace RVS.Cust_Intake.MudDemo.State;

/// <summary>
/// Mock state container for the 7-step intake wizard demo.
/// Uses hardcoded data — no API calls or persistence.
/// </summary>
public sealed class IntakeWizardState
{
    private const int TotalStepCount = 7;

    public int CurrentStep { get; private set; } = 1;
    public int TotalSteps => TotalStepCount;

    // Mock dealer/location info
    public string DealershipName => "Mountain View RV Center";
    public string LocationName => "Salt Lake City Service Bay";

    // Step 2 — Customer Info
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    // Step 3 — Asset Info
    public string Vin { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }

    // Step 4 — Issue Description
    public string? IssueCategory { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public string? Urgency { get; set; }
    public string? RvUsage { get; set; }

    // Step 5 — Diagnostic Questions
    public List<MockDiagnosticQuestion> DiagnosticQuestions { get; set; } = [];
    public List<MockDiagnosticResponse> DiagnosticResponses { get; set; } = [];
    public string? SmartSuggestion { get; set; }

    // Step 6 — Attachments
    public List<MockAttachment> Attachments { get; set; } = [];

    // Submission
    public bool IsSubmitted { get; set; }

    public event Action? OnChange;

    public void GoToNextStep()
    {
        if (CurrentStep < TotalStepCount)
        {
            CurrentStep++;
            OnChange?.Invoke();
        }
    }

    public void GoToPreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            OnChange?.Invoke();
        }
    }

    public void GoToStep(int step)
    {
        if (step >= 1 && step <= TotalStepCount)
        {
            CurrentStep = step;
            OnChange?.Invoke();
        }
    }

    public void LoadMockDiagnosticQuestions()
    {
        if (DiagnosticQuestions.Count > 0) return;

        DiagnosticQuestions =
        [
            new("When did you first notice the issue?",
                ["Today", "This week", "This month", "More than a month ago"], true),
            new("Has this happened before?",
                ["Yes", "No", "Not sure"], false),
            new("Were any warning lights or indicators visible?",
                ["Check Engine", "Battery", "Electrical", "None", "Not sure"], true),
        ];

        DiagnosticResponses = DiagnosticQuestions
            .Select(q => new MockDiagnosticResponse(q.QuestionText))
            .ToList();

        SmartSuggestion = "Based on your description, this may be related to the electrical system. "
            + "Please answer the following questions to help our technicians prepare.";
    }

    public List<string> ValidateCurrentStep() => CurrentStep switch
    {
        1 => [],
        2 => ValidateCustomerInfo(),
        3 => ValidateAssetInfo(),
        4 => ValidateIssueDescription(),
        _ => []
    };

    private List<string> ValidateCustomerInfo()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(FirstName)) errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(LastName)) errors.Add("Last name is required.");
        if (string.IsNullOrWhiteSpace(Email)) errors.Add("Email is required.");
        return errors;
    }

    private List<string> ValidateAssetInfo()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Vin)) errors.Add("VIN is required.");
        else if (Vin.Trim().Length != 17) errors.Add("VIN must be exactly 17 characters.");
        return errors;
    }

    private List<string> ValidateIssueDescription()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(IssueCategory)) errors.Add("Issue category is required.");
        if (string.IsNullOrWhiteSpace(IssueDescription)) errors.Add("Issue description is required.");
        return errors;
    }
}

public sealed record MockDiagnosticQuestion(
    string QuestionText,
    List<string> Options,
    bool AllowFreeText);

public sealed class MockDiagnosticResponse(string questionText)
{
    public string QuestionText { get; set; } = questionText;
    public List<string> SelectedOptions { get; set; } = [];
    public string? FreeTextResponse { get; set; }
}

public sealed class MockAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
