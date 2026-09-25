using MudBlazor;
using RVS.Domain.DTOs;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// How the detail dialog presents service-packet generation (<c>Spec C-2</c>, issue #443):
/// the label and alert severity for each state, when Regenerate is offered, and the state
/// shown once a regeneration has been accepted.
/// </summary>
public static class PacketStatusDisplay
{
    /// <summary>Short, manager-facing label for the packet state.</summary>
    public static string GetLabel(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.Status switch
        {
            "Pending" => "Queued",
            "Generating" => "Generating",
            "Succeeded" => "Ready",
            "Failed" when packet.RetriesExhausted => "Failed",
            "Failed" => $"Retrying (attempt {packet.AttemptCount} of {packet.MaxAttempts})",
            _ => packet.Status
        };
    }

    /// <summary>
    /// Alert severity for the packet state. A failure with retries left is a warning — the
    /// worker will try again; only an exhausted failure is an error.
    /// </summary>
    public static Severity GetSeverity(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.Status switch
        {
            "Succeeded" => Severity.Success,
            "Failed" when packet.RetriesExhausted => Severity.Error,
            "Failed" => Severity.Warning,
            _ => Severity.Info
        };
    }

    /// <summary>True when generation failed and the worker has given up (<c>Spec B-1</c>).</summary>
    public static bool IsFailedExhausted(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.Status == "Failed" && packet.RetriesExhausted;
    }

    /// <summary>
    /// Regenerate is offered once a run has finished, either way. It is withheld while a run is
    /// queued or generating, where it would only restart work already in flight.
    /// </summary>
    public static bool CanRegenerate(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.Status is "Failed" or "Succeeded";
    }

    /// <summary>
    /// True once any packet version has been generated. A later failed or in-flight
    /// regeneration keeps the last good PDF, so the link stays available.
    /// </summary>
    public static bool HasPdf(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet.PacketVersion > 0;
    }

    /// <summary>
    /// The state after the API accepts a regeneration — mirrors
    /// <c>PacketGenerationEmbedded.ResetForRegeneration</c>: back to <c>Pending</c> with the
    /// attempt count cleared; the last good version and its timestamp are kept.
    /// </summary>
    public static PacketGenerationDto AsPendingRegeneration(PacketGenerationDto packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet with { Status = "Pending", AttemptCount = 0, RetriesExhausted = false };
    }
}
