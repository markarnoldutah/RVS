using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.UI.Shared.Components;

/// <summary>
/// How much weight a status carries on screen. Deliberately not a MudBlazor type:
/// <c>RVS.UI.Shared</c> does not reference MudBlazor, so each app maps these to its own
/// <c>Color</c> or <c>Severity</c>.
/// </summary>
public enum IntakeInviteStatusTone
{
    /// <summary>Nothing is pending and nothing went wrong.</summary>
    Neutral,

    /// <summary>Still in flight.</summary>
    Progress,

    /// <summary>It got there.</summary>
    Success,

    /// <summary>It did not.</summary>
    Error
}

/// <summary>
/// What an invite reads as in the Send intake link dialog (<c>Spec A-14</c>, issue #666).
/// </summary>
/// <param name="Label">The line shown to the advisor.</param>
/// <param name="Tone">How much weight it carries.</param>
public sealed record IntakeInviteStatusDisplay(string Label, IntakeInviteStatusTone Tone);

/// <summary>
/// Pure presentation helper for the Send intake link dialog's recent-sends list
/// (<c>Spec A-14</c>, issue #666).
///
/// It translates the stored <c>IntakeInviteDeliveryStatus</c> vocabulary into the words a
/// service advisor uses, and lets two facts outrank it: a submitted form makes the carrier's
/// verdict moot, and an expired link is worth saying even when it was delivered.
/// </summary>
public static class IntakeInviteStatusFormatting
{
    /// <summary>
    /// Describes one invite as of <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="invite">The invite.</param>
    /// <param name="utcNow">Current UTC time, for the expiry check.</param>
    public static IntakeInviteStatusDisplay Describe(IntakeInviteSummaryResponseDto invite, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invite);

        // The customer came back. Nothing about delivery or expiry changes that.
        if (invite.RedeemedAtUtc is not null)
        {
            return new IntakeInviteStatusDisplay("Form submitted", IntakeInviteStatusTone.Success);
        }

        if (invite.ExpiresAtUtc <= utcNow)
        {
            return new IntakeInviteStatusDisplay("Expired", IntakeInviteStatusTone.Neutral);
        }

        return invite.DeliveryStatus switch
        {
            IntakeInviteDeliveryStatus.Pending => new("Sending…", IntakeInviteStatusTone.Progress),
            IntakeInviteDeliveryStatus.Queued => new("Sent", IntakeInviteStatusTone.Progress),
            IntakeInviteDeliveryStatus.Delivered => new("Delivered", IntakeInviteStatusTone.Success),
            IntakeInviteDeliveryStatus.Failed => new("Not delivered", IntakeInviteStatusTone.Error),
            IntakeInviteDeliveryStatus.NotSent => new("Not texted", IntakeInviteStatusTone.Neutral),
            _ when string.IsNullOrWhiteSpace(invite.DeliveryStatus) =>
                new("Unknown", IntakeInviteStatusTone.Neutral),
            _ => new(invite.DeliveryStatus, IntakeInviteStatusTone.Neutral)
        };
    }

    /// <summary>
    /// Whether the carrier still owes a delivery report — the dialog re-reads the invite while
    /// this is <c>true</c>, and stops as soon as it isn't.
    /// </summary>
    /// <param name="invite">The invite.</param>
    public static bool IsAwaitingDeliveryReport(IntakeInviteSummaryResponseDto invite)
    {
        ArgumentNullException.ThrowIfNull(invite);

        return invite.DeliveryStatus is IntakeInviteDeliveryStatus.Pending
            or IntakeInviteDeliveryStatus.Queued;
    }

    /// <summary>
    /// Reads a stored E.164 number back the way it was dialled. Anything that is not a North
    /// American number passes through untouched, and a missing one becomes an em dash.
    /// </summary>
    /// <param name="e164">The stored phone number.</param>
    public static string FormatPhone(string? e164)
    {
        if (string.IsNullOrWhiteSpace(e164))
        {
            return "—";
        }

        // +1AAABBBCCCC — the only shape A-14 stores, since intake and invites are US/Canada.
        if (e164.Length == 12 && e164.StartsWith("+1", StringComparison.Ordinal) &&
            e164.AsSpan(2).ContainsOnlyDigits())
        {
            return $"({e164[2..5]}) {e164[5..8]}-{e164[8..]}";
        }

        return e164;
    }

    private static bool ContainsOnlyDigits(this ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
