using System;
using System.Collections.Generic;

namespace RVS.Domain.Integrations.Availity;

/// <summary>
/// Request model for Availity eligibility check (POST /v1/coverages).
/// Maps to x-www-form-urlencoded parameters per Availity Coverages API.
/// </summary>
public sealed record AvailityEligibilityRequest
{
    // =====================================================
    // Payer Information
    // =====================================================

    /// <summary>
    /// The Availity-specific payer identifier.
    /// </summary>
    public required string PayerId { get; init; }

    // =====================================================
    // Provider Information
    // =====================================================

    /// <summary>
    /// Requesting provider's NPI (National Provider Identifier).
    /// Most payers require this.
    /// </summary>
    public string? ProviderNpi { get; init; }

    /// <summary>
    /// Requesting provider's last name or organization name.
    /// </summary>
    public string? ProviderLastName { get; init; }

    /// <summary>
    /// Requesting provider's first name (if individual).
    /// </summary>
    public string? ProviderFirstName { get; init; }

    /// <summary>
    /// Requesting provider's tax ID (some payers require this).
    /// </summary>
    public string? ProviderTaxId { get; init; }

    /// <summary>
    /// Payer-assigned provider ID (if applicable).
    /// </summary>
    public string? PayerAssignedProviderId { get; init; }

    /// <summary>
    /// Submitter ID for the practice/organization.
    /// </summary>
    public string? SubmitterId { get; init; }

    // =====================================================
    // Subscriber/Member Information
    // =====================================================

    /// <summary>
    /// Patient's health plan member ID number.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Patient's health plan group number.
    /// </summary>
    public string? GroupNumber { get; init; }

    /// <summary>
    /// Subscriber's first name.
    /// </summary>
    public string? SubscriberFirstName { get; init; }

    /// <summary>
    /// Subscriber's last name.
    /// </summary>
    public string? SubscriberLastName { get; init; }

    /// <summary>
    /// Subscriber's date of birth.
    /// </summary>
    public DateTime? SubscriberDob { get; init; }

    // =====================================================
    // Patient Information (if different from subscriber)
    // =====================================================

    /// <summary>
    /// Patient's first name.
    /// </summary>
    public string? PatientFirstName { get; init; }

    /// <summary>
    /// Patient's last name.
    /// </summary>
    public string? PatientLastName { get; init; }

    /// <summary>
    /// Patient's date of birth.
    /// </summary>
    public DateTime? PatientBirthDate { get; init; }

    /// <summary>
    /// Patient's gender (M, F, U).
    /// </summary>
    public string? PatientGender { get; init; }

    /// <summary>
    /// Patient's relationship to subscriber (18=Self, 01=Spouse, 19=Child, G8=Other).
    /// </summary>
    public string? SubscriberRelationship { get; init; }

    // =====================================================
    // Service Information
    // =====================================================

    /// <summary>
    /// Date of service for which coverage is being verified.
    /// </summary>
    public required DateTime DateOfService { get; init; }

    /// <summary>
    /// End date for coverage search (optional, for date ranges).
    /// </summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Service type codes (X12 STC list, e.g., "30" for Health Benefit Plan Coverage).
    /// If not specified, defaults to general health benefits inquiry.
    /// </summary>
    public List<string>? ServiceTypeCodes { get; init; }
}
