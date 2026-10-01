using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Spots the API's "tenant disabled" 403 (<see cref="TenantAccessDeniedResponseDto.TenantDisabledCode"/>)
/// on any call and records it in <see cref="TenantAccessState"/> (issue #625). The response is
/// passed back unchanged, so callers behave as before; the layout does the rest.
/// Any other 403 — a missing permission, a missing tenant claim — is left alone.
/// </summary>
public sealed class TenantAccessGateHandler : DelegatingHandler
{
    private readonly TenantAccessState _state;

    /// <summary>
    /// Initializes a new instance of <see cref="TenantAccessGateHandler"/>.
    /// </summary>
    public TenantAccessGateHandler(TenantAccessState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Forbidden || response.Content is null)
        {
            return response;
        }

        // Buffer first so the caller can still read the body.
        await response.Content.LoadIntoBufferAsync(cancellationToken);

        TenantAccessDeniedResponseDto? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<TenantAccessDeniedResponseDto>(cancellationToken);
        }
        catch (JsonException)
        {
            return response;
        }

        if (body?.Code == TenantAccessDeniedResponseDto.TenantDisabledCode)
        {
            _state.MarkRestricted(body.DisabledMessage, body.SupportContactEmail);
        }

        return response;
    }
}
