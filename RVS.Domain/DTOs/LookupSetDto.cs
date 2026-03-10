namespace RVS.Domain.DTOs;

public sealed record LookupSetDto(
    string Category,         // e.g. "encounter-types" (from entity Id)
    string Name,
    IReadOnlyList<LookupItemDto> Items
);


