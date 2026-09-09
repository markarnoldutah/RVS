namespace RVS.Domain.DTOs;

/// <summary>
/// Request body for setting or clearing the manager-authored customer status note
/// (<c>Spec C-9</c>). Send a null, empty, or whitespace-only <see cref="Note"/> to clear the
/// current note. A non-blank note is capped at 280 characters and sanitised on the server
/// (blocked characters <c>&lt; &gt; ; ' " \ \0</c>).
/// </summary>
public sealed record ServiceRequestStatusNoteRequestDto
{
    /// <summary>The note text to show the customer, or null/blank to clear it.</summary>
    public string? Note { get; init; }
}
