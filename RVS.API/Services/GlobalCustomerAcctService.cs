using System.Security.Cryptography;
using System.Text;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing <see cref="GlobalCustomerAcct"/> entities.
/// Cross-tenant — resolves by email, generates magic-link tokens,
/// and links tenant-scoped customer profiles.
/// </summary>
public sealed class GlobalCustomerAcctService : IGlobalCustomerAcctService
{
    private readonly IGlobalCustomerAcctRepository _repository;
    private readonly IUserContextAccessor _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalCustomerAcctService"/>.
    /// </summary>
    public GlobalCustomerAcctService(IGlobalCustomerAcctRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
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
    public async Task<GlobalCustomerAcct> GenerateMagicLinkTokenAsync(string email, DateTime? expiresAtUtc = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var account = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new KeyNotFoundException($"Global customer account for email '{normalizedEmail}' not found.");

        var token = GenerateMagicLinkToken(normalizedEmail);
        account.MagicLinkToken = token;
        account.MagicLinkExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(30);
        account.MarkAsUpdated(_userContext.UserId);

        return await _repository.UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GlobalCustomerAcct> ValidateMagicLinkTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var account = await _repository.GetByMagicLinkTokenAsync(token, cancellationToken)
            ?? throw new KeyNotFoundException("No account found for the provided magic-link token.");

        if (account.MagicLinkExpiresAtUtc.HasValue && account.MagicLinkExpiresAtUtc.Value < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Magic-link token has expired.");
        }

        return account;
    }

    /// <summary>
    /// Generates a magic-link token in the format <c>base64url(SHA256(email)[0..8]):random_bytes</c>.
    /// The email-hash prefix enables O(1) partition-key derivation on read.
    /// </summary>
    internal static string GenerateMagicLinkToken(string email)
    {
        var emailHash = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        var prefix = Convert.ToBase64String(emailHash[..8])
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var randomBytes = new byte[16];
        RandomNumberGenerator.Fill(randomBytes);
        var suffix = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{prefix}:{suffix}";
    }
}
