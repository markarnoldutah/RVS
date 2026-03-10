using RVS.Domain.DTOs;

namespace RVS.BlazorWASM.Features.CheckIn;

/// <summary>
/// Possible states for the patient discovery workflow.
/// </summary>
public enum PatientDiscoveryState
{
    Searching,
    Loading,
    NoResults,
    ResultsDisplayed,
    PatientSelected,
    CreatingNewPatient,
    EditingPatient
}

/// <summary>
/// State container for the PatientDiscoverySection component.
/// All state is private and can only be modified through defined methods.
/// </summary>
public class PatientDiscoverySectionState
{
    // Private backing fields - state can only be changed via methods
    private PatientDiscoveryState _discoveryState = PatientDiscoveryState.Searching;
    private string _searchLastName = string.Empty;
    private string _searchFirstName = string.Empty;
    private DateOnly? _searchDateOfBirth;
    private string? _searchMemberId;
    private List<PatientSearchResultResponseDto> _searchResults = [];
    private string? _selectedPatientId;
    private PatientDetailResponseDto? _selectedPatient;
    private PatientCheckInDemographicsDto _demographics = new()
    {
        FirstName = string.Empty,
        LastName = string.Empty
    };

    // Read-only properties for accessing state
    public PatientDiscoveryState DiscoveryState => _discoveryState;
    public string SearchLastName => _searchLastName;
    public string SearchFirstName => _searchFirstName;
    public DateOnly? SearchDateOfBirth => _searchDateOfBirth;
    public string? SearchMemberId => _searchMemberId;
    public IReadOnlyList<PatientSearchResultResponseDto> SearchResults => _searchResults;
    public string? SelectedPatientId => _selectedPatientId;
    public PatientDetailResponseDto? SelectedPatient => _selectedPatient;
    public PatientCheckInDemographicsDto Demographics => _demographics;

    /// <summary>
    /// Whether a patient is currently selected (existing patient).
    /// </summary>
    public bool HasSelectedPatient => !string.IsNullOrWhiteSpace(_selectedPatientId);

    /// <summary>
    /// Whether the user is creating a new patient.
    /// </summary>
    public bool IsCreatingNew => _discoveryState == PatientDiscoveryState.CreatingNewPatient;

    // State mutation methods

    /// <summary>
    /// Sets the discovery state.
    /// </summary>
    public void SetDiscoveryState(PatientDiscoveryState state)
    {
        _discoveryState = state;
    }

    /// <summary>
    /// Updates the search last name.
    /// </summary>
    public void SetSearchLastName(string lastName)
    {
        _searchLastName = lastName;
    }

    /// <summary>
    /// Updates the search first name.
    /// </summary>
    public void SetSearchFirstName(string firstName)
    {
        _searchFirstName = firstName;
    }

    /// <summary>
    /// Updates the search date of birth.
    /// </summary>
    public void SetSearchDateOfBirth(DateOnly? dateOfBirth)
    {
        _searchDateOfBirth = dateOfBirth;
    }

    /// <summary>
    /// Updates the search member ID.
    /// </summary>
    public void SetSearchMemberId(string? memberId)
    {
        _searchMemberId = memberId;
    }

    /// <summary>
    /// Sets the search results.
    /// </summary>
    public void SetSearchResults(List<PatientSearchResultResponseDto> results)
    {
        _searchResults = results;
    }

    /// <summary>
    /// Updates demographics.
    /// </summary>
    public void SetDemographics(PatientCheckInDemographicsDto demographics)
    {
        _demographics = demographics;
    }

    /// <summary>
    /// Populates state from a loaded patient.
    /// </summary>
    public void PopulateFromPatient(PatientDetailResponseDto patient)
    {
        _selectedPatientId = patient.PatientId;
        _selectedPatient = patient;

        _demographics = new PatientCheckInDemographicsDto
        {
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Email = patient.Email,
            Phone = patient.Phone
        };
    }

    /// <summary>
    /// Prepares state for creating a new patient using search criteria.
    /// </summary>
    public void PrepareNewPatientFromSearch()
    {
        _selectedPatientId = null;
        _selectedPatient = null;

        _demographics = new PatientCheckInDemographicsDto
        {
            FirstName = _searchFirstName,
            LastName = _searchLastName,
            DateOfBirth = _searchDateOfBirth
        };
    }

    /// <summary>
    /// Resets search state.
    /// </summary>
    public void ResetSearch()
    {
        _discoveryState = PatientDiscoveryState.Searching;
        _selectedPatientId = null;
        _selectedPatient = null;
        _searchResults = [];
    }

    /// <summary>
    /// Resets entire state.
    /// </summary>
    public void Reset()
    {
        _discoveryState = PatientDiscoveryState.Searching;
        _searchLastName = string.Empty;
        _searchFirstName = string.Empty;
        _searchDateOfBirth = null;
        _searchMemberId = null;
        _searchResults = [];
        _selectedPatientId = null;
        _selectedPatient = null;
        _demographics = new() { FirstName = string.Empty, LastName = string.Empty };
    }
}
