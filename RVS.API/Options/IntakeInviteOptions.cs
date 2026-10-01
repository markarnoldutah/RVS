using System.ComponentModel.DataAnnotations;

namespace RVS.API.Options;

/// <summary>
/// Advisor intake invite configuration (<c>Spec A-14</c>, issue #663). Bound from the
/// <c>IntakeInvites</c> section and validated at startup. The Spec makes the rate caps
/// configuration, not constants.
/// </summary>
public sealed class IntakeInviteOptions
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "IntakeInvites";

    /// <summary>How long an invite's link works after it is created. The Spec says about 72 hours.</summary>
    [Range(1, 24 * 30)]
    public int ExpiryHours { get; set; } = 72;

    /// <summary>Texted invites one advisor may send in any rolling hour.</summary>
    [Range(1, int.MaxValue)]
    public int MaxPerAdvisorPerHour { get; set; } = 20;

    /// <summary>Texted invites one location may send in any rolling hour, across all its advisors.</summary>
    [Range(1, int.MaxValue)]
    public int MaxPerLocationPerHour { get; set; } = 60;

    /// <summary>Texted invites one tenant may send in any rolling hour, across all its locations.</summary>
    [Range(1, int.MaxValue)]
    public int MaxPerTenantPerHour { get; set; } = 100;

    /// <summary>How far back the send dialog's recent-sends list reaches: one shift.</summary>
    [Range(1, 24 * 7)]
    public int RecentWindowHours { get; set; } = 12;

    /// <summary>Most invites the recent-sends list returns.</summary>
    [Range(1, 100)]
    public int RecentMaxItems { get; set; } = 50;
}
