using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QRCoder;
using RVS.API.Mappers;
using RVS.API.Options;
using RVS.API.Services;
using RVS.Domain.DTOs;
using RVS.Domain.Interfaces;
using RVS.Domain.Links;
using RVS.Domain.Validation;

namespace RVS.API.Controllers;

/// <summary>
/// Manages location entities for multi-location dealerships.
/// </summary>
[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _service;
    private readonly ClaimsService _claimsService;
    private readonly IntakeUrlOptions _intakeUrlOptions;

    /// <summary>
    /// Initializes a new instance of <see cref="LocationsController"/>.
    /// </summary>
    public LocationsController(
        ILocationService service,
        ClaimsService claimsService,
        IOptions<IntakeUrlOptions> intakeUrlOptions)
    {
        _service = service;
        _claimsService = claimsService;
        _intakeUrlOptions = intakeUrlOptions.Value;
    }

    /// <summary>
    /// Lists all locations belonging to the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<IReadOnlyList<LocationSummaryResponseDto>>> List(CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entities = await _service.ListByTenantAsync(tenantId, ct);

        return Ok(entities.Select(e => e.ToSummaryDto()).ToList());
    }

    /// <summary>
    /// Gets a single location by its identifier.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<LocationDetailDto>> GetById(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);

        return Ok(entity.ToDetailDto());
    }

    /// <summary>
    /// Creates a new location.
    /// </summary>
    /// <param name="request">Location creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "CanCreateLocations")]
    public async Task<ActionResult<LocationDetailDto>> Create(
        [FromBody] LocationCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        var userId = _claimsService.GetUserIdOrThrow();

        var entity = request.ToEntity(tenantId, userId);
        var created = await _service.CreateAsync(tenantId, entity, ct);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDetailDto());
    }

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="request">Location update request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanUpdateLocations")]
    public async Task<ActionResult<LocationDetailDto>> Update(
        string id, [FromBody] LocationCreateRequestDto request, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();
        var userId = _claimsService.GetUserIdOrThrow();

        var existing = await _service.GetByIdAsync(tenantId, id, ct);
        existing.ApplyUpdate(request, userId);
        var updated = await _service.UpdateAsync(tenantId, id, existing, ct);

        return Ok(updated.ToDetailDto());
    }

    /// <summary>
    /// Generates and returns a QR code PNG image for the location's intake form URL.
    ///
    /// The code encodes the <c>go.rvintake.com</c> short link tagged <c>src=qr</c>
    /// (<c>Spec A-13</c>, issue #599), not the intake URL directly — a scan that bypassed the
    /// redirect would be a scan nobody could count, and every sticker already printed would
    /// stay uncounted for as long as it is on a counter.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}/qr-code")]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<IActionResult> GetQrCode(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);
        var intakeUrl = IntakeLinkBuilder.ShortLink(
            _intakeUrlOptions.RedirectOrIntakeBaseUrl, entity.Slug, IntakeSourceVocabulary.Qr);

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(intakeUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(20);

        return File(pngBytes, "image/png", $"qr-{entity.Slug}.png");
    }

    /// <summary>
    /// Returns the location's channel-tagged intake links — the short link to print, the one
    /// encoded in the QR sticker, and the ones to paste into a Text Replacement snippet or a
    /// canned quick reply (<c>Spec A-13</c>, issue #599).
    ///
    /// Exists so the links a dealer hands out are copied from one place rather than typed from
    /// memory: a hand-built link that skips the redirect is a channel that silently stops being
    /// measured.
    /// </summary>
    /// <param name="id">Location identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id}/intake-links")]
    [Authorize(Policy = "CanReadLocations")]
    public async Task<ActionResult<LocationIntakeLinksResponseDto>> GetIntakeLinks(string id, CancellationToken ct)
    {
        var tenantId = _claimsService.GetTenantIdOrThrow();

        var entity = await _service.GetByIdAsync(tenantId, id, ct);
        var baseUrl = _intakeUrlOptions.RedirectOrIntakeBaseUrl;

        return Ok(new LocationIntakeLinksResponseDto
        {
            LocationId = entity.Id,
            Slug = entity.Slug,
            PrintUrl = IntakeLinkBuilder.ShortLink(baseUrl, entity.Slug),
            QrUrl = IntakeLinkBuilder.ShortLink(baseUrl, entity.Slug, IntakeSourceVocabulary.Qr),
            TextReplacementUrl = IntakeLinkBuilder.ShortLink(baseUrl, entity.Slug, IntakeSourceVocabulary.TextReplacement),
            QuickReplyUrl = IntakeLinkBuilder.ShortLink(baseUrl, entity.Slug, IntakeSourceVocabulary.QuickReply),
            ManagerAppUrl = IntakeLinkBuilder.ShortLink(baseUrl, entity.Slug, IntakeSourceVocabulary.ManagerApp)
        });
    }
}
