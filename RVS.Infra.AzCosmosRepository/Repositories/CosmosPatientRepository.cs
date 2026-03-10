using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;
using RVS.Domain.Validation;
using Microsoft.Azure.Cosmos;
using System.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace RVS.Infra.AzCosmosRepository.Repositories
{
    /// <summary>
    /// Cosmos DB repository for Patient aggregate with embedded encounters.
    /// 
    /// Partition Key: /practiceId (practice-scoped for HIPAA compliance)
    /// 
    /// Document structure:
    /// Patient
    /// ??? CoverageEnrollments[]
    /// ??? Encounters[]
    ///     ??? CoverageDecision
    ///     ??? EligibilityChecks[]
    ///         ??? CoverageLines[]
    ///         ??? Payloads[]
    /// </summary>
    public class CosmosPatientRepository : CosmosRepositoryBase, IPatientRepository
    {
        private readonly Container _container;

        public CosmosPatientRepository(
            CosmosClient client,
            string databaseId,
            string containerId) : base(client)
        {
            _container = GetContainer(databaseId, containerId);
        }

        // =====================================================
        // Patient Operations
        // =====================================================

        public async Task<Patient?> GetByIdAsync(string tenantId, string practiceId, string patientId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(patientId);

            try
            {
                // Point read using practiceId as partition key
                var response = await _container.ReadItemAsync<Patient>(
                    id: patientId,
                    partitionKey: new PartitionKey(practiceId));

                var patient = response.Resource;

                // Enforce tenant isolation (partition is practice, so validate tenant)
                if (!string.Equals(patient.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    return null;

                return patient;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task CreateAsync(Patient patient)
        {
            ArgumentNullException.ThrowIfNull(patient);
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.TenantId, nameof(patient.TenantId));
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.PracticeId, nameof(patient.PracticeId));
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.Id, nameof(patient.Id));

            // No need to set audit properties here - they are already set in the service layer

            await _container.CreateItemAsync(
                patient,
                partitionKey: new PartitionKey(patient.PracticeId));
        }

        public async Task UpdateAsync(Patient patient)
        {
            ArgumentNullException.ThrowIfNull(patient);
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.TenantId, nameof(patient.TenantId));
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.PracticeId, nameof(patient.PracticeId));
            ArgumentException.ThrowIfNullOrWhiteSpace(patient.Id, nameof(patient.Id));

            // No need to set audit properties here - MarkAsUpdated() should be called in service layer

            await _container.ReplaceItemAsync(
                patient,
                id: patient.Id,
                partitionKey: new PartitionKey(patient.PracticeId));
        }

        public async Task<PagedResult<Patient>> SearchAsync(
            string tenantId,
            string practiceId,
            PatientSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(practiceId);
            ArgumentNullException.ThrowIfNull(request);

            // Validate search parameters
            var validationResult = PatientSearchValidator.Validate(
                request.LastName,
                request.FirstName,
                request.MemberId,
                request.PageSize,
                request.ContinuationToken);

            if (!validationResult.IsValid)
            {
                throw new ArgumentException(
                    $"Invalid search parameters: {validationResult.GetErrorMessage()}", 
                    nameof(request));
            }

            // Projection query for list view - only lightweight fields (~0.5KB per patient)
            // Avoids over-fetching full 124KB documents
            const string selectClause = @"SELECT c.id, c.patientId, c.tenantId, c.practiceId, c.firstName, c.lastName, 
                               c.dateOfBirth, c.email, c.phone, c.isEnabled, c.createdAtUtc, c.updatedAtUtc
                        FROM c";

            // Build WHERE clause and parameters together to avoid duplication
            var predicates = new List<string>
            {
                "c.practiceId = @practiceId",
                "c.tenantId = @tenantId"
            };
            var parameters = new Dictionary<string, object>
            {
                ["@practiceId"] = practiceId,
                ["@tenantId"] = tenantId
            };

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                predicates.Add("STARTSWITH(c.lastName, @lastName, true)");
                parameters["@lastName"] = PatientSearchValidator.SanitizeSearchTerm(request.LastName);
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                predicates.Add("STARTSWITH(c.firstName, @firstName, true)");
                parameters["@firstName"] = PatientSearchValidator.SanitizeSearchTerm(request.FirstName);
            }

            if (request.DateOfBirth.HasValue)
            {
                predicates.Add("c.dateOfBirth = @dateOfBirth");
                parameters["@dateOfBirth"] = request.DateOfBirth.Value.ToString("yyyy-MM-dd");
            }

            // MemberId search requires scanning embedded CoverageEnrollments array
            if (!string.IsNullOrWhiteSpace(request.MemberId))
            {
                predicates.Add("EXISTS(SELECT VALUE ce FROM ce IN c.coverageEnrollments WHERE CONTAINS(LOWER(ce.memberId), @memberId))");
                parameters["@memberId"] = PatientSearchValidator.SanitizeSearchTerm(request.MemberId).ToLowerInvariant();
            }

            // ORDER BY ensures consistent pagination with continuation tokens
            var sql = $"{selectClause} WHERE {string.Join(" AND ", predicates)} ORDER BY c.lastName, c.firstName, c.id";
            
            var queryDef = new QueryDefinition(sql);
            foreach (var param in parameters)
            {
                queryDef = queryDef.WithParameter(param.Key, param.Value);
            }

            var options = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(practiceId),
                MaxItemCount = request.PageSize
            };

            var iterator = _container.GetItemQueryIterator<Patient>(
                queryDef,
                continuationToken: request.ContinuationToken,
                requestOptions: options);

            var items = new List<Patient>();
            string? newToken = null;

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                newToken = response.ContinuationToken;
                items.AddRange(response.Resource);
            }

            return new PagedResult<Patient>
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = 0,
                ContinuationToken = newToken
            };
        }

        // =====================================================
        // Coverage Enrollment Operations (Embedded Documents)
        // =====================================================

        public async Task<CoverageEnrollmentEmbedded?> GetCoverageEnrollmentAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string coverageEnrollmentId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(coverageEnrollmentId);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            return patient?.CoverageEnrollments?
                .FirstOrDefault(c => c.CoverageEnrollmentId == coverageEnrollmentId);
        }

        public async Task<CoverageEnrollmentEmbedded> AddCoverageEnrollmentAsync(
            string tenantId,
            string practiceId,
            string patientId,
            CoverageEnrollmentEmbedded newCoverage)
        {
            ArgumentNullException.ThrowIfNull(newCoverage);
            ArgumentException.ThrowIfNullOrWhiteSpace(newCoverage.CoverageEnrollmentId, nameof(newCoverage.CoverageEnrollmentId));

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            patient.CoverageEnrollments ??= [];
            patient.CoverageEnrollments.Add(newCoverage);

            await UpdateAsync(patient);
            return newCoverage;
        }

        public async Task<CoverageEnrollmentEmbedded> UpdateCoverageEnrollmentAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string coverageEnrollmentId,
            Action<CoverageEnrollmentEmbedded> updateAction)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(coverageEnrollmentId);
            ArgumentNullException.ThrowIfNull(updateAction);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            patient.CoverageEnrollments ??= [];

            var existing = patient.CoverageEnrollments.FirstOrDefault(c => c.CoverageEnrollmentId == coverageEnrollmentId);
            if (existing == null)
                throw new KeyNotFoundException($"Coverage enrollment {coverageEnrollmentId} not found.");

            ArgumentException.ThrowIfNullOrWhiteSpace(existing.CoverageEnrollmentId, nameof(existing.CoverageEnrollmentId));
            updateAction(existing);
            ArgumentException.ThrowIfNullOrWhiteSpace(existing.CoverageEnrollmentId, nameof(existing.CoverageEnrollmentId));

            await UpdateAsync(patient);
            return existing;
        }

        public async Task DeleteCoverageEnrollmentAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string coverageEnrollmentId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(coverageEnrollmentId);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            patient.CoverageEnrollments ??= [];

            var removed = patient.CoverageEnrollments.RemoveAll(c => c.CoverageEnrollmentId == coverageEnrollmentId);
            if (removed == 0)
                throw new KeyNotFoundException($"Coverage enrollment {coverageEnrollmentId} not found.");

            await UpdateAsync(patient);
        }

        // =====================================================
        // Encounter Operations (Embedded Documents)
        // =====================================================

        public async Task<EncounterEmbedded?> GetEncounterAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            return patient?.Encounters?.FirstOrDefault(e => e.Id == encounterId);
        }

        public async Task<List<EncounterEmbedded>> GetEncountersAsync(
            string tenantId,
            string practiceId,
            string patientId,
            PatientEncounterSearchRequestDto? request = null)
        {
            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                return [];

            var encounters = patient.Encounters ?? [];

            // Apply optional filters
            if (request != null)
            {
                // Convert DateOnly to DateTime for comparison with VisitDate (which is DateTime in UTC)
                if (request.FromDate.HasValue)
                {
                    var fromDateTime = request.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    encounters = encounters.Where(e => e.VisitDate >= fromDateTime).ToList();
                }

                if (request.ToDate.HasValue)
                {
                    // Include the entire end date by using end of day
                    var toDateTime = request.ToDate.Value.ToDateTime(new TimeOnly(23, 59, 59, 999), DateTimeKind.Utc);
                    encounters = encounters.Where(e => e.VisitDate <= toDateTime).ToList();
                }

                if (!string.IsNullOrWhiteSpace(request.VisitType))
                    encounters = encounters.Where(e => string.Equals(e.VisitType, request.VisitType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Sort by visit date descending (client-side, efficient for small arrays ~8 avg)
            return encounters.OrderByDescending(e => e.VisitDate).ToList();
        }

        public async Task<EncounterEmbedded> AddEncounterAsync(
            string tenantId,
            string practiceId,
            string patientId,
            EncounterEmbedded newEncounter)
        {
            ArgumentNullException.ThrowIfNull(newEncounter);
            ArgumentException.ThrowIfNullOrWhiteSpace(newEncounter.Id, nameof(newEncounter.Id));

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            patient.Encounters ??= [];
            patient.Encounters.Add(newEncounter);

            await UpdateAsync(patient);
            return newEncounter;
        }

        public async Task<EncounterEmbedded> UpdateEncounterAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            Action<EncounterEmbedded> updateAction)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
            ArgumentNullException.ThrowIfNull(updateAction);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            patient.Encounters ??= [];

            var encounter = patient.Encounters.FirstOrDefault(e => e.Id == encounterId);
            if (encounter == null)
                throw new KeyNotFoundException($"Encounter {encounterId} not found.");

            updateAction(encounter);
            // NOTE: UpdatedAtUtc and UpdatedByUserId for encounter are now set in service layer

            await UpdateAsync(patient);
            return encounter;
        }

        // =====================================================
        // Coverage Decision Operations (Embedded in Encounter)
        // =====================================================

        public async Task<CoverageDecisionEmbedded> SetCoverageDecisionAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            CoverageDecisionEmbedded decision)
        {
            ArgumentNullException.ThrowIfNull(decision);

            var encounter = await UpdateEncounterAsync(tenantId, practiceId, patientId, encounterId, e =>
            {
                e.CoverageDecision = decision;
            });

            return encounter.CoverageDecision!;
        }

        public async Task<CoverageDecisionEmbedded?> GetCoverageDecisionAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId)
        {
            var encounter = await GetEncounterAsync(tenantId, practiceId, patientId, encounterId);
            return encounter?.CoverageDecision;
        }

        // =====================================================
        // Eligibility Check Operations (Embedded in Encounter)
        // =====================================================

        public async Task<EligibilityCheckEmbedded> AddEligibilityCheckAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            EligibilityCheckEmbedded newCheck)
        {
            ArgumentNullException.ThrowIfNull(newCheck);
            ArgumentException.ThrowIfNullOrWhiteSpace(newCheck.EligibilityCheckId, nameof(newCheck.EligibilityCheckId));

            if (newCheck.RequestedAtUtc == default)
                newCheck.RequestedAtUtc = DateTime.UtcNow;

            await UpdateEncounterAsync(tenantId, practiceId, patientId, encounterId, encounter =>
            {
                encounter.EligibilityChecks ??= [];
                encounter.EligibilityChecks.Add(newCheck);
            });

            return newCheck;
        }

        public async Task<EligibilityCheckEmbedded> UpdateEligibilityCheckAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            string eligibilityCheckId,
            Action<EligibilityCheckEmbedded> updateAction)
        {
            ArgumentNullException.ThrowIfNull(updateAction);
            ArgumentException.ThrowIfNullOrWhiteSpace(eligibilityCheckId);

            var patient = await GetByIdAsync(tenantId, practiceId, patientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient {patientId} not found.");

            var encounter = patient.Encounters?.FirstOrDefault(e => e.Id == encounterId);
            if (encounter == null)
                throw new KeyNotFoundException($"Encounter {encounterId} not found.");

            encounter.EligibilityChecks ??= [];
            var check = encounter.EligibilityChecks.FirstOrDefault(c => c.EligibilityCheckId == eligibilityCheckId);
            if (check == null)
                throw new KeyNotFoundException($"Eligibility check {eligibilityCheckId} not found.");

            ArgumentException.ThrowIfNullOrWhiteSpace(check.EligibilityCheckId, nameof(check.EligibilityCheckId));
            updateAction(check);
            ArgumentException.ThrowIfNullOrWhiteSpace(check.EligibilityCheckId, nameof(check.EligibilityCheckId));

            encounter.UpdatedAtUtc = DateTime.UtcNow;
            await UpdateAsync(patient);

            return check;
        }

        public async Task<EligibilityCheckEmbedded?> GetEligibilityCheckAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            string eligibilityCheckId)
        {
            var encounter = await GetEncounterAsync(tenantId, practiceId, patientId, encounterId);
            return encounter?.EligibilityChecks?.FirstOrDefault(c => c.EligibilityCheckId == eligibilityCheckId);
        }

        public async Task<List<EligibilityCheckEmbedded>> GetEligibilityChecksAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId)
        {
            var encounter = await GetEncounterAsync(tenantId, practiceId, patientId, encounterId);
            return encounter?.EligibilityChecks ?? [];
        }

        public Task AddCoverageLineAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            string eligibilityCheckId,
            CoverageLineEmbedded coverageLine)
        {
            ArgumentNullException.ThrowIfNull(coverageLine);

            return UpdateEligibilityCheckAsync(
                tenantId,
                practiceId,
                patientId,
                encounterId,
                eligibilityCheckId,
                check =>
                {
                    check.CoverageLines ??= [];
                    check.CoverageLines.Add(coverageLine);
                });
        }

        public Task AddEligibilityPayloadAsync(
            string tenantId,
            string practiceId,
            string patientId,
            string encounterId,
            string eligibilityCheckId,
            EligibilityPayloadEmbedded payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentException.ThrowIfNullOrWhiteSpace(payload.PayloadId, nameof(payload.PayloadId));

            // Note: CreatedAtUtc is set via init property default value in entity

            return UpdateEligibilityCheckAsync(
                tenantId,
                practiceId,
                patientId,
                encounterId,
                eligibilityCheckId,
                check =>
                {
                    check.Payloads ??= [];
                    check.Payloads.Add(payload);
                });
        }
    }
}
