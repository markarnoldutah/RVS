using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RVS.Domain.DTOs;

namespace RVS.UI.Shared.Services;

/// <summary>
/// Typed HTTP client for advisor intake invites (<c>Spec A-14</c>, issues #663 and #666), backing
/// the manager app's Send intake link dialog. Routes map to
/// <c>api/locations/{locationId}/intake-invites</c>.
///
/// Every refusal the advisor can act on — texting not enabled, the number opted out, a rate
/// limit, a missing permission — arrives as a plain-language <see cref="IntakeInviteApiException"/>,
/// so the dialog can show the API's own sentence rather than an HTTP error.
/// </summary>
public sealed class IntakeInviteApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="IntakeInviteApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/> injected via DI.</param>
    public IntakeInviteApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Creates an invite: texts the caller a prefilled intake link, or — with
    /// <see cref="IntakeInviteCreateRequestDto.SelfEntry"/> — mints one for the advisor to open,
    /// which works while texting is disabled.
    /// </summary>
    /// <param name="locationId">Location whose intake form the invite opens.</param>
    /// <param name="request">The invite to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IntakeInviteApiException">The API refused the invite.</exception>
    public async Task<IntakeInviteDetailResponseDto> SendAsync(
        string locationId,
        IntakeInviteCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            BaseRoute(locationId), request, cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IntakeInviteDetailResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize intake invite response.");
    }

    /// <summary>
    /// The current advisor's invites at this location for the current shift, newest first.
    /// </summary>
    /// <param name="locationId">Location the invites were created for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IntakeInviteApiException">The API refused the read.</exception>
    public async Task<List<IntakeInviteSummaryResponseDto>> ListRecentAsync(
        string locationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        using var response = await _httpClient.GetAsync(BaseRoute(locationId), cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<IntakeInviteSummaryResponseDto>>(
            cancellationToken: cancellationToken) ?? [];
    }

    /// <summary>
    /// Re-reads one invite, which is how the dialog picks up the delivery report.
    /// </summary>
    /// <param name="locationId">Location the invite belongs to.</param>
    /// <param name="inviteId">Invite id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IntakeInviteApiException">The API refused the read.</exception>
    public async Task<IntakeInviteDetailResponseDto> GetAsync(
        string locationId,
        string inviteId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inviteId);

        using var response = await _httpClient.GetAsync(
            $"{BaseRoute(locationId)}/{Uri.EscapeDataString(inviteId)}", cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IntakeInviteDetailResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize intake invite response.");
    }

    /// <summary>
    /// Whether the API will actually text an invite in this environment. Read when the dialog
    /// opens, so it can offer only <i>Fill it in myself</i> while texting is off.
    /// </summary>
    /// <param name="locationId">Location the dialog was opened for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IntakeInviteApiException">The API refused the read.</exception>
    public async Task<IntakeInviteCapabilityResponseDto> GetCapabilityAsync(
        string locationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        using var response = await _httpClient.GetAsync(
            $"{BaseRoute(locationId)}/capability", cancellationToken);

        await ThrowIfRefusedAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IntakeInviteCapabilityResponseDto>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize intake invite capability response.");
    }

    private static string BaseRoute(string locationId) =>
        $"api/locations/{Uri.EscapeDataString(locationId)}/intake-invites";

    /// <summary>
    /// Turns a refusal into a sentence an advisor can act on. <c>ExceptionHandlingMiddleware</c>
    /// puts the service's own message in ProblemDetails' <c>detail</c>; a 403 is produced by the
    /// authorization middleware and has no body at all, so its wording lives here.
    /// </summary>
    private static async Task ThrowIfRefusedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        throw new IntakeInviteApiException(
            response.StatusCode,
            detail ?? DefaultMessageFor(response.StatusCode));
    }

    private static async Task<string?> ReadProblemDetailAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
            {
                var text = detail.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch (JsonException)
        {
            // An empty or non-JSON body is normal for 403 and for gateway errors.
        }

        return null;
    }

    private static string DefaultMessageFor(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Your session has expired. Sign in again and try once more.",
        HttpStatusCode.Forbidden =>
            "Your role doesn't have permission to send intake links. Ask an owner to add the "
            + "service advisor or service manager role to your account.",
        HttpStatusCode.NotFound => "That location or invite no longer exists.",
        HttpStatusCode.Conflict => "That invite can't be sent right now.",
        HttpStatusCode.TooManyRequests => "Too many intake links sent in the last hour. Try again later.",
        _ => "Something went wrong sending the intake link. Try again."
    };
}

/// <summary>
/// An intake-invite call the API refused, carrying a message meant to be shown to the advisor
/// as-is (<c>Spec A-14</c>, issue #666).
/// </summary>
public sealed class IntakeInviteApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="IntakeInviteApiException"/>.
    /// </summary>
    /// <param name="statusCode">The status the API answered with.</param>
    /// <param name="message">A message safe to show the advisor.</param>
    public IntakeInviteApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>The status the API answered with.</summary>
    public HttpStatusCode StatusCode { get; }
}
