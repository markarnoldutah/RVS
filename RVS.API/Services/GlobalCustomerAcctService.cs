using RVS.Domain.Entities;
using RVS.Domain.Exceptions;
using RVS.Domain.Interfaces;
using RVS.Domain.Security;
using RVS.Domain.Shared;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="GlobalCustomerAcct"/> entities.
/// Cross-tenant — resolves by email, issues per-customer status tokens (Spec X-5),
/// and links tenant-scoped customer profiles.
/// </summary>
public sealed class GlobalCustomerAcctService : IGlobalCustomerAcctService
{
    /// <summary>Status-token TTL cap (Spec X-5: ≤ 30 days).</summary>
    internal const int StatusTokenTtlDays = 30;

    /// <summary>
    /// Sliding-renewal threshold — a validated token is pushed back to the full TTL only once its
    /// remaining lifetime drops below this, so the status page does not write on every load.
    /// </summary>
    internal const int RenewalThresholdDays = 29;

    private readonly IGlobalCustomerAcctRepository _repository;
    private readonly IUserContextAccessor _userContext;
    private readonly ILogger<GlobalCustomerAcctService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalCustomerAcctService"/>.
    /// </summary>
    public GlobalCustomerAcctService(
        IGlobalCustomerAcctRepository repository,
        IUserContextAccessor userContext,
        ILogger<GlobalCustomerAcctService> logger)
    {
        _repository = repository;
        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _repository.GetByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new KeyNotFoundException($"Global customer account for email '{normalizedEmail}' not found.");
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> GetOrCreateAsync(string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var account = new GlobalCustomerAcct
        {
            Email = normalizedEmail,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            CreatedByUserId = _userContext.UserId,
        };

        return await _repository.CreateAsync(account, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> LinkProfileAsync(string identityId, string tenantId, string profileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var account = await _repository.GetByIdAsync(identityId, cancellationToken)
            ?? throw new KeyNotFoundException($"Global customer account '{identityId}' not found.");

        var alreadyLinked = account.LinkedProfiles
            .Any(lp => lp.TenantId == tenantId && lp.ProfileId == profileId);

        if (!alreadyLinked)
        {
            account.LinkedProfiles.Add(new LinkedProfileEmbedded
            {
                TenantId = tenantId,
                ProfileId = profileId,
                FirstSeenAtUtc = DateTime.UtcNow,
                RequestCount = 1,
            });

            account.MarkAsUpdated(_userContext.UserId);
            return await _repository.UpdateAsync(account, cancellationToken);
        }

        return account;
    }

    /// <inheritdoc />
    public async Task<MagicLinkIssueResult> GenerateMagicLinkTokenAsync(string email, DateTime? expiresAtUtc = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var account = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new KeyNotFoundException($"Global customer account for email '{normalizedEmail}' not found.");

        var rawToken = AnonymousTokenHelper.GenerateStatusToken(normalizedEmail);
        account.MagicLinkTokenHash = AnonymousTokenHelper.ComputeHash(rawToken);
        account.MagicLinkExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(StatusTokenTtlDays);
        account.MarkAsUpdated(_userContext.UserId);

        var saved = await _repository.UpdateAsync(account, cancellationToken);
        return new MagicLinkIssueResult(saved, rawToken);
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> ValidateMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var tokenHash = AnonymousTokenHelper.ComputeHash(token);
        var account = await _repository.GetByMagicLinkTokenHashAsync(tokenHash, cancellationToken);

        if (account is null)
        {
            _logger.LogInformation(
                "Anonymous status token validation: outcome={Outcome}, tokenHash={TokenHash}",
                "miss", tokenHash);
            throw new KeyNotFoundException("No account found for the provided status token.");
        }

        var emailHash = AnonymousTokenHelper.ComputeHash(account.Email);
        var tenantIds = string.Join(",", account.LinkedProfiles.Select(p => p.TenantId).Distinct());

        if (account.MagicLinkExpiresAtUtc.HasValue && account.MagicLinkExpiresAtUtc.Value < DateTime.UtcNow)
        {
            _logger.LogInformation(
                "Anonymous status token validation: outcome={Outcome}, emailHash={EmailHash}, tenantIds={TenantIds}",
                "expired", emailHash, tenantIds);
            throw new MagicLinkExpiredException("Status token has expired.");
        }

        // Sliding renewal — extend the TTL only once the token is within the renewal window,
        // so a returning visitor keeps a live link without a write on every status-page load.
        if (!account.MagicLinkExpiresAtUtc.HasValue ||
            account.MagicLinkExpiresAtUtc.Value < DateTime.UtcNow.AddDays(RenewalThresholdDays))
        {
            account.MagicLinkExpiresAtUtc = DateTime.UtcNow.AddDays(StatusTokenTtlDays);
            account.MarkAsUpdated(_userContext.UserId);
            account = await _repository.UpdateAsync(account, cancellationToken);
        }

        _logger.LogInformation(
            "Anonymous status token validation: outcome={Outcome}, emailHash={EmailHash}, tenantIds={TenantIds}",
            "hit", emailHash, tenantIds);

        return account;
    }
}
