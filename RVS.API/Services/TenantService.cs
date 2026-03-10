using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RVS.API.Mappers;
using RVS.Domain.DTOs;
using RVS.Domain.Entities;
using RVS.Domain.Interfaces;

namespace RVS.API.Services;

/// <summary>
/// Service for managing tenant configuration and settings.
/// </summary>
public class TenantService : ITenantService
{
    private readonly IConfigRepository _configRepository;
    private readonly IUserContextAccessor _userContext;

    public TenantService(IConfigRepository configRepository, IUserContextAccessor userContext)
    {
        _configRepository = configRepository;
        _userContext = userContext;
    }

    public async Task<TenantConfig> CreateTenantConfigAsync(string tenantId, TenantConfigCreateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var existingConfig = await _configRepository.GetTenantConfigAsync(tenantId);
        if (existingConfig != null)
            throw new InvalidOperationException($"Tenant config already exists for tenant {tenantId}.");

        var tenantConfig = request.ToEntity(tenantId, _userContext.UserId);
        await _configRepository.CreateTenantConfigAsync(tenantConfig);

        return tenantConfig;
    }

    public async Task<TenantConfig> GetTenantConfigAsync(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var tenantCfg = await _configRepository.GetTenantConfigAsync(tenantId);
        if (tenantCfg is null)
            throw new KeyNotFoundException("Tenant config not found.");

        return tenantCfg;
    }

    public async Task<TenantConfig> UpdateTenantConfigAsync(string tenantId, TenantConfigUpdateRequestDto request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var tenantCfg = await _configRepository.GetTenantConfigAsync(tenantId);
        if (tenantCfg is null)
            throw new KeyNotFoundException("Tenant config not found.");

        tenantCfg.ApplyUpdateFromDto(request);
        tenantCfg.MarkAsUpdated(_userContext.UserId);
        await _configRepository.SaveTenantConfigAsync(tenantCfg);

        return tenantCfg;
    }

    public async Task<TenantAccessGate> GetAccessGateAsync(string tenantId)
    {
        var cfg = await GetTenantConfigAsync(tenantId);

        // Safe default: allow logins if access gate is missing.
        return cfg.AccessGate ?? new TenantAccessGate { LoginsEnabled = true };
    }
}
