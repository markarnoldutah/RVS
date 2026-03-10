namespace RVS.Domain.DTOs;

public sealed record LookupItemDto(
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsSelectable
);






