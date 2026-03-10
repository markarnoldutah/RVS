using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RVS.Domain.Integrations.Availity;
using RVS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RVS.API.Integrations.Availity;

/// <summary>
/// Typed HttpClient wrapper for Availity eligibility (Coverages API).
/// Uses .NET's built-in HttpClientFactory + standard resilience handler (retry/backoff/timeout).
/// 
/// Implements async polling pattern:
/// 1. InitiateCoverageCheckAsync - POST /v1/coverages
/// 2. PollCoverageStatusAsync - GET /v1/coverages/{id}
/// </summary>
public sealed class AvailityEligibilityClient : IAvailityEligibilityClient
{
    private readonly HttpClient _http;
    private readonly AvailityOptions _options;
    private readonly ILogger<AvailityEligibilityClient>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AvailityEligibilityClient(
        HttpClient http,
        IOptions<AvailityOptions> options,
        ILogger<AvailityEligibilityClient>? logger = null)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AvailityInitiateResponse> InitiateCoverageCheckAsync(
        AvailityEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = NormalizePath(_options.EligibilityPath);

        // Build form content per Availity Coverages API spec (x-www-form-urlencoded)
        var formContent = BuildFormContent(request);

        _logger?.LogDebug("Initiating coverage check for payer {PayerId}, member {MemberId}",
            request.PayerId, MaskMemberId(request.MemberId));

        try
        {
            var response = await _http.PostAsync(path, formContent, cancellationToken);

            // Read response body for parsing
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Availity POST failed with {StatusCode}: {Body}",
                    response.StatusCode, TruncateForLog(body));

                // Try to parse validation errors from 400 responses
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorResponse = TryParseErrorResponse(body);
                    if (errorResponse is not null)
                    {
                        return new AvailityInitiateResponse
                        {
                            CoverageId = "",
                            StatusCode = "19",
                            Status = "Request Error",
                            ValidationMessages = errorResponse.Errors,
                            ErrorMessage = errorResponse.UserMessage ?? "Validation failed"
                        };
                    }
                }

                return new AvailityInitiateResponse
                {
                    CoverageId = "",
                    StatusCode = ((int)response.StatusCode).ToString(),
                    Status = "Failed",
                    ErrorMessage = $"Availity returned {response.StatusCode}: {TruncateForLog(body)}"
                };
            }

            // Parse successful response
            var parsed = JsonSerializer.Deserialize<AvailityRawCoverageResponse>(body, JsonOptions);

            if (parsed is null || string.IsNullOrEmpty(parsed.Id))
            {
                _logger?.LogWarning("Availity returned empty or unparseable response");
                return new AvailityInitiateResponse
                {
                    CoverageId = "",
                    StatusCode = "0",
                    Status = "Unknown",
                    ErrorMessage = "Empty or invalid response from Availity"
                };
            }

            _logger?.LogInformation("Coverage check initiated: {CoverageId}, status={StatusCode}",
                parsed.Id, parsed.StatusCode);

            var initiateResponse = new AvailityInitiateResponse
            {
                CoverageId = parsed.Id,
                StatusCode = parsed.StatusCode ?? "0",
                Status = parsed.Status ?? "In Progress",
                EtaDate = parsed.EtaDate,
                ValidationMessages = MapValidationMessages(parsed.ValidationMessages)
            };

            // If immediately complete, parse the result (rare but possible)
            if (parsed.StatusCode is "4" or "3")
            {
                initiateResponse = initiateResponse with
                {
                    Result = MapToEligibilityResult(parsed)
                };
            }

            return initiateResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error initiating coverage check");
            return new AvailityInitiateResponse
            {
                CoverageId = "",
                StatusCode = "7",
                Status = "Communication Error",
                ErrorMessage = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning(ex, "Timeout initiating coverage check");
            return new AvailityInitiateResponse
            {
                CoverageId = "",
                StatusCode = "7",
                Status = "Communication Error",
                ErrorMessage = "Request timed out"
            };
        }
    }

    /// <inheritdoc />
    public async Task<AvailityPollResponse> PollCoverageStatusAsync(
        string availityCoverageId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(availityCoverageId);

        var path = $"{NormalizePath(_options.EligibilityPath)}/{availityCoverageId}";

        _logger?.LogDebug("Polling coverage status: {CoverageId}", availityCoverageId);

        try
        {
            var response = await _http.GetAsync(path, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Availity GET failed with {StatusCode}", response.StatusCode);

                return new AvailityPollResponse
                {
                    StatusCode = ((int)response.StatusCode).ToString(),
                    Status = "Communication Error",
                    ErrorMessage = $"Availity returned {response.StatusCode}"
                };
            }

            var parsed = JsonSerializer.Deserialize<AvailityRawCoverageResponse>(body, JsonOptions);

            if (parsed is null)
            {
                return new AvailityPollResponse
                {
                    StatusCode = "13",
                    Status = "Communication Error",
                    ErrorMessage = "Invalid response from Availity"
                };
            }

            _logger?.LogInformation("Poll result: StatusCode={StatusCode}, Status={Status}, HasResult={HasResult}",
                parsed.StatusCode, parsed.Status, parsed.Plans?.Count > 0);

            // Map to our response model
            var pollResponse = new AvailityPollResponse
            {
                StatusCode = parsed.StatusCode ?? "0",
                Status = parsed.Status ?? "Unknown",
                EtaDate = parsed.EtaDate,
                ValidationMessages = MapValidationMessages(parsed.ValidationMessages)
            };

            // If complete, parse the full result
            if (pollResponse.IsComplete)
            {
                pollResponse = pollResponse with
                {
                    Result = MapToEligibilityResult(parsed)
                };
            }
            else if (pollResponse.IsFailed)
            {
                pollResponse = pollResponse with
                {
                    ErrorMessage = GetErrorMessage(parsed)
                };
            }

            return pollResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error polling coverage status");
            return new AvailityPollResponse
            {
                StatusCode = "7",
                Status = "Communication Error",
                ErrorMessage = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning(ex, "Timeout polling coverage status");
            return new AvailityPollResponse
            {
                StatusCode = "7",
                Status = "Communication Error",
                ErrorMessage = "Request timed out"
            };
        }
    }

    // =====================================================
    // Private Helper Methods
    // =====================================================

    private static string NormalizePath(string path)
    {
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static FormUrlEncodedContent BuildFormContent(AvailityEligibilityRequest request)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("payerId", request.PayerId),
            new("memberId", request.MemberId),
            new("asOfDate", request.DateOfService.ToString("yyyy-MM-dd"))
        };

        // Provider info
        if (!string.IsNullOrWhiteSpace(request.ProviderNpi))
            fields.Add(new("providerNpi", request.ProviderNpi));
        if (!string.IsNullOrWhiteSpace(request.ProviderLastName))
            fields.Add(new("providerLastName", request.ProviderLastName));
        if (!string.IsNullOrWhiteSpace(request.ProviderFirstName))
            fields.Add(new("providerFirstName", request.ProviderFirstName));
        if (!string.IsNullOrWhiteSpace(request.ProviderTaxId))
            fields.Add(new("providerTaxId", request.ProviderTaxId));
        if (!string.IsNullOrWhiteSpace(request.SubmitterId))
            fields.Add(new("submitterId", request.SubmitterId));

        // Member/subscriber info
        if (!string.IsNullOrWhiteSpace(request.GroupNumber))
            fields.Add(new("groupNumber", request.GroupNumber));
        if (!string.IsNullOrWhiteSpace(request.SubscriberLastName))
            fields.Add(new("patientLastName", request.SubscriberLastName));
        if (!string.IsNullOrWhiteSpace(request.SubscriberFirstName))
            fields.Add(new("patientFirstName", request.SubscriberFirstName));
        if (request.SubscriberDob.HasValue)
            fields.Add(new("patientBirthDate", request.SubscriberDob.Value.ToString("yyyy-MM-dd")));

        // Patient info (if different from subscriber)
        if (!string.IsNullOrWhiteSpace(request.PatientLastName))
            fields.Add(new("patientLastName", request.PatientLastName));
        if (!string.IsNullOrWhiteSpace(request.PatientFirstName))
            fields.Add(new("patientFirstName", request.PatientFirstName));
        if (request.PatientBirthDate.HasValue)
            fields.Add(new("patientBirthDate", request.PatientBirthDate.Value.ToString("yyyy-MM-dd")));
        if (!string.IsNullOrWhiteSpace(request.PatientGender))
            fields.Add(new("patientGender", request.PatientGender));
        if (!string.IsNullOrWhiteSpace(request.SubscriberRelationship))
            fields.Add(new("subscriberRelationship", request.SubscriberRelationship));

        // Service types (array format: serviceType[]=30&serviceType[]=33)
        if (request.ServiceTypeCodes is { Count: > 0 })
        {
            foreach (var stc in request.ServiceTypeCodes)
            {
                fields.Add(new("serviceType[]", stc));
            }
        }
        else
        {
            // Default to health benefit plan coverage (30)
            fields.Add(new("serviceType[]", "30"));
        }

        if (request.ToDate.HasValue)
            fields.Add(new("toDate", request.ToDate.Value.ToString("yyyy-MM-dd")));

        return new FormUrlEncodedContent(fields);
    }

    private static AvailityEligibilityResult? MapToEligibilityResult(AvailityRawCoverageResponse raw)
    {
        if (raw.Plans is null || raw.Plans.Count == 0)
            return null;

        var plan = raw.Plans[0]; // Primary plan

        var result = new AvailityEligibilityResult
        {
            PlanName = plan.PlanName,
            GroupNumber = plan.GroupNumber,
            GroupName = plan.GroupName,
            InsuranceType = plan.InsuranceType,
            EligibilityStartDate = plan.EligibilityStartDate,
            EligibilityEndDate = plan.EligibilityEndDate,
            CoverageStartDate = plan.CoverageStartDate,
            CoverageEndDate = plan.CoverageEndDate,
            Subscriber = raw.Subscriber is not null ? new AvailitySubscriberInfo
            {
                MemberId = raw.Subscriber.MemberId,
                FirstName = raw.Subscriber.FirstName,
                LastName = raw.Subscriber.LastName,
                BirthDate = raw.Subscriber.BirthDate,
                Gender = raw.Subscriber.Gender
            } : null,
            Patient = raw.Patient is not null ? new AvailityPatientInfo
            {
                FirstName = raw.Patient.FirstName,
                LastName = raw.Patient.LastName,
                BirthDate = raw.Patient.BirthDate,
                Gender = raw.Patient.Gender,
                SubscriberRelationship = raw.Patient.SubscriberRelationship,
                SubscriberRelationshipCode = raw.Patient.SubscriberRelationshipCode
            } : null,
            CoverageLines = MapCoverageLines(plan),
            PayerNotes = plan.PayerNotes?.ConvertAll(n => n.Message ?? "")
        };

        return result;
    }

    private static List<AvailityCoverageLine> MapCoverageLines(AvailityRawPlan plan)
    {
        var lines = new List<AvailityCoverageLine>();

        if (plan.Benefits is null)
            return lines;

        foreach (var benefit in plan.Benefits)
        {
            // Map copay, deductible, coinsurance from amounts
            if (benefit.Amounts?.CoPay?.InNetwork is { Count: > 0 })
            {
                foreach (var item in benefit.Amounts.CoPay.InNetwork)
                {
                    lines.Add(new AvailityCoverageLine
                    {
                        ServiceTypeCode = benefit.Name ?? "30",
                        ServiceTypeDescription = benefit.Type,
                        CoverageType = "Copay",
                        Network = "InNetwork",
                        Amount = item.Amount,
                        TimePeriod = item.AmountTimePeriod,
                        Level = item.Level,
                        AuthorizationRequired = item.AuthorizationRequired
                    });
                }
            }

            if (benefit.Amounts?.Deductibles?.InNetwork is { Count: > 0 })
            {
                foreach (var item in benefit.Amounts.Deductibles.InNetwork)
                {
                    lines.Add(new AvailityCoverageLine
                    {
                        ServiceTypeCode = benefit.Name ?? "30",
                        ServiceTypeDescription = benefit.Type,
                        CoverageType = "Deductible",
                        Network = "InNetwork",
                        Amount = item.Amount,
                        TimePeriod = item.AmountTimePeriod,
                        Level = item.Level
                    });
                }
            }

            if (benefit.Amounts?.CoInsurance?.InNetwork is { Count: > 0 })
            {
                foreach (var item in benefit.Amounts.CoInsurance.InNetwork)
                {
                    lines.Add(new AvailityCoverageLine
                    {
                        ServiceTypeCode = benefit.Name ?? "30",
                        ServiceTypeDescription = benefit.Type,
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = item.Amount,
                        TimePeriod = item.AmountTimePeriod,
                        Level = item.Level
                    });
                }
            }
        }

        return lines;
    }

    private static List<AvailityValidationMessage>? MapValidationMessages(
        List<AvailityRawValidationMessage>? raw)
    {
        if (raw is null || raw.Count == 0)
            return null;

        return raw.ConvertAll(m => new AvailityValidationMessage
        {
            Field = m.Field,
            Code = m.Code,
            ErrorMessage = m.ErrorMessage,
            Index = m.Index
        });
    }

    private static string? GetErrorMessage(AvailityRawCoverageResponse raw)
    {
        if (raw.ValidationMessages is { Count: > 0 })
        {
            return string.Join("; ", raw.ValidationMessages.ConvertAll(m => m.ErrorMessage ?? m.Code ?? "Unknown error"));
        }
        return raw.Status;
    }

    private static AvailityRawErrorResponse? TryParseErrorResponse(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<AvailityRawErrorResponse>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string MaskMemberId(string memberId)
    {
        if (string.IsNullOrEmpty(memberId) || memberId.Length <= 4)
            return "****";
        return new string('*', memberId.Length - 4) + memberId[^4..];
    }

    private static string TruncateForLog(string? value, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    // =====================================================
    // Raw Availity Response Models (internal)
    // =====================================================

    private sealed record AvailityRawCoverageResponse
    {
        public string? Id { get; init; }
        public string? CustomerId { get; init; }
        public string? Status { get; init; }
        public string? StatusCode { get; init; }
        public DateTime? EtaDate { get; init; }
        public DateTime? CreatedDate { get; init; }
        public DateTime? UpdatedDate { get; init; }
        public DateTime? ExpirationDate { get; init; }
        public AvailityRawSubscriber? Subscriber { get; init; }
        public AvailityRawPatient? Patient { get; init; }
        public AvailityRawPayer? Payer { get; init; }
        public List<AvailityRawPlan>? Plans { get; init; }
        public List<AvailityRawValidationMessage>? ValidationMessages { get; init; }
    }

    private sealed record AvailityRawErrorResponse
    {
        public string? UserMessage { get; init; }
        public string? DeveloperMessage { get; init; }
        public int? StatusCode { get; init; }
        public List<AvailityValidationMessage>? Errors { get; init; }
    }

    private sealed record AvailityRawSubscriber
    {
        public string? MemberId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? Gender { get; init; }
    }

    private sealed record AvailityRawPatient
    {
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? Gender { get; init; }
        public string? SubscriberRelationship { get; init; }
        public string? SubscriberRelationshipCode { get; init; }
    }

    private sealed record AvailityRawPayer
    {
        public string? PayerId { get; init; }
        public string? Name { get; init; }
    }

    private sealed record AvailityRawPlan
    {
        public string? Status { get; init; }
        public string? StatusCode { get; init; }
        public string? GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public string? PlanName { get; init; }
        public string? InsuranceType { get; init; }
        public DateTime? EligibilityStartDate { get; init; }
        public DateTime? EligibilityEndDate { get; init; }
        public DateTime? CoverageStartDate { get; init; }
        public DateTime? CoverageEndDate { get; init; }
        public List<AvailityRawBenefit>? Benefits { get; init; }
        public List<AvailityRawPayerNote>? PayerNotes { get; init; }
    }

    private sealed record AvailityRawBenefit
    {
        public string? Name { get; init; }
        public string? Type { get; init; }
        public string? Status { get; init; }
        public AvailityRawBenefitAmounts? Amounts { get; init; }
    }

    private sealed record AvailityRawBenefitAmounts
    {
        public AvailityRawNetworkBenefits? CoPay { get; init; }
        public AvailityRawNetworkBenefits? Deductibles { get; init; }
        public AvailityRawNetworkBenefits? CoInsurance { get; init; }
        public AvailityRawNetworkBenefits? OutOfPocket { get; init; }
    }

    private sealed record AvailityRawNetworkBenefits
    {
        public List<AvailityRawBenefitDetail>? InNetwork { get; init; }
        public List<AvailityRawBenefitDetail>? OutOfNetwork { get; init; }
    }

    private sealed record AvailityRawBenefitDetail
    {
        public string? Status { get; init; }
        public string? Amount { get; init; }
        public string? Remaining { get; init; }
        public string? AmountTimePeriod { get; init; }
        public string? Level { get; init; }
        public bool? AuthorizationRequired { get; init; }
    }

    private sealed record AvailityRawPayerNote
    {
        public string? Type { get; init; }
        public string? Message { get; init; }
    }

    private sealed record AvailityRawValidationMessage
    {
        public string? Field { get; init; }
        public string? Code { get; init; }
        public string? ErrorMessage { get; init; }
        public int? Index { get; init; }
    }
}
